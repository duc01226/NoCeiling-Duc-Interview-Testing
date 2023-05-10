using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Exceptions.Extensions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs;

public interface IPlatformCqrsEventApplicationHandler
{
    bool IsCurrentInstanceHandlingEventFromInboxBusMessage { get; set; }
    Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken);
    Task Handle(object notification, CancellationToken cancellationToken);
}

public interface IPlatformCqrsEventApplicationHandler<in TEvent> : IPlatformCqrsEventApplicationHandler, IPlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken);
}

public abstract class PlatformCqrsEventApplicationHandler<TEvent> : PlatformCqrsEventHandler<TEvent>, IPlatformCqrsEventApplicationHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager) : base(loggerFactory)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    protected virtual bool AutoOpenUow => true;

    /// <summary>
    /// Default return True. When True, Support for store cqrs event handler as inbox if inbox bus message is enabled in persistence module
    /// </summary>
    protected virtual bool EnableHandleEventFromInboxBusMessage => true;

    public bool IsCurrentInstanceHandlingEventFromInboxBusMessage { get; set; }

    public Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken)
    {
        return DoExecuteHandleAsync(@event.As<TEvent>(), cancellationToken);
    }

    public Task Handle(object notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification.As<TEvent>(), cancellationToken);
    }

    public override Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification, cancellationToken);
    }

    public override async Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        await DoExecuteHandleAsync(@event, cancellationToken);
    }

    private Task DoHandle(TEvent notification, CancellationToken cancellationToken)
    {
        return base.Handle(notification, cancellationToken);
    }

    private async Task DoExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        var hasInboxMessageRepository = PlatformApplicationGlobal.RootServiceProvider
            .ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);

        if (EnableHandleEventFromInboxBusMessage &&
            hasInboxMessageRepository &&
            !IsCurrentInstanceHandlingEventFromInboxBusMessage)
        {
            await PlatformApplicationGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                async (
                    IServiceProvider serviceProvider,
                    IPlatformInboxBusMessageRepository inboxMessageRepository,
                    IPlatformApplicationSettingContext applicationSettingContext,
                    IPlatformApplicationUserContextAccessor userContextAccessor) =>
                {
                    var consumerInstance = serviceProvider.GetRequiredService<PlatformCqrsEventInboxBusMessageConsumer>();

                    await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                        serviceProvider: serviceProvider,
                        consumer: consumerInstance,
                        inboxBusMessageRepository: inboxMessageRepository,
                        message: CqrsEventInboxBusMessage(@event, eventHandlerType: GetType(), applicationSettingContext, userContextAccessor),
                        routingKey: PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(typeof(TEvent), applicationSettingContext.ApplicationName),
                        Logger,
                        retryProcessFailedMessageInSecondsUnit: PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                        cancellationToken: cancellationToken);
                });
        }
        else
        {
            if (AutoOpenUow)
                using (var uow = UnitOfWorkManager.Begin())
                {
                    await HandleAsync(@event, cancellationToken);
                    await uow.CompleteAsync(cancellationToken);
                }
            else
                await HandleAsync(@event, cancellationToken);
        }
    }

    protected virtual PlatformBusMessage<PlatformCqrsEventBusMessagePayload> CqrsEventInboxBusMessage(
        TEvent @event,
        Type eventHandlerType,
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformApplicationUserContextAccessor userContextAccessor)
    {
        return PlatformBusMessage<PlatformCqrsEventBusMessagePayload>.New<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>(
            trackId: $"{Guid.NewGuid()}-{eventHandlerType.Name}",
            payload: PlatformCqrsEventBusMessagePayload.New(@event, eventHandlerType.FullName),
            identity: new PlatformBusMessageIdentity
            {
                UserId = userContextAccessor.Current.UserId(),
                RequestId = userContextAccessor.Current.RequestId(),
                UserName = userContextAccessor.Current.UserName()
            },
            producerContext: applicationSettingContext.ApplicationName,
            messageGroup: nameof(PlatformCqrsEvent),
            messageAction: @event.EventAction);
    }
}

public class PlatformCqrsEventInboxBusMessageConsumer : PlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>
{
    public PlatformCqrsEventInboxBusMessageConsumer(ILoggerFactory loggerFactory, IUnitOfWorkManager uowManager, IServiceProvider serviceProvider) : base(
        loggerFactory,
        uowManager,
        serviceProvider)
    {
    }

    public override async Task HandleLogicAsync(PlatformBusMessage<PlatformCqrsEventBusMessagePayload> message, string routingKey)
    {
        await ServiceProvider.ExecuteInjectScopedAsync(
            async (IServiceProvider serviceProvider) =>
            {
                var scanAssemblies = serviceProvider.GetServices<PlatformModule>()
                    .Select(p => p.Assembly)
                    .ConcatSingle(typeof(PlatformModule).Assembly)
                    .ToList();

                var eventHandlerType = scanAssemblies
                    .Select(p => p.GetType(message.Payload.EventHandlerTypeFullName))
                    .FirstOrDefault(p => p != null)
                    .EnsureFound(errorMsg: $"Not found defined event handler. EventHandlerType:{message.Payload.EventHandlerTypeFullName}")
                    .Ensure(
                        must: p => p.FindMatchedGenericType(typeof(IPlatformCqrsEventApplicationHandler<>)) != null,
                        $"Handler {message.Payload.EventHandlerTypeFullName} must extended from {typeof(IPlatformCqrsEventApplicationHandler<>).FullName}");

                var eventHandlerInstance = serviceProvider.GetRequiredService(eventHandlerType).As<IPlatformCqrsEventApplicationHandler>()
                    .With(_ => _.IsCurrentInstanceHandlingEventFromInboxBusMessage = true);
                var eventType = scanAssemblies
                    .Select(p => p.GetType(message.Payload.EventTypeFullName))
                    .FirstOrDefault(p => p != null);

                await eventHandlerInstance.ExecuteHandleAsync(PlatformJsonSerializer.Deserialize(message.Payload.EventJson, eventType), CancellationToken.None);
            });
    }
}

public class PlatformCqrsEventBusMessagePayload
{
    public string EventJson { get; set; }
    public string EventTypeFullName { get; set; }
    public string EventHandlerTypeFullName { get; set; }

    public static PlatformCqrsEventBusMessagePayload New<TEvent>(TEvent @event, string eventHandlerTypeFullName)
        where TEvent : PlatformCqrsEvent, new()
    {
        return new PlatformCqrsEventBusMessagePayload
        {
            EventJson = @event.ToJson(),
            EventTypeFullName = @event.GetType().FullName,
            EventHandlerTypeFullName = eventHandlerTypeFullName
        };
    }
}
