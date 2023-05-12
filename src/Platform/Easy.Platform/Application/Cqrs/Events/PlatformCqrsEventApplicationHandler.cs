using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Events.InboxSupport;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public interface IPlatformCqrsEventApplicationHandler
{
    bool IsCurrentInstanceCalledFromInboxBusMessageConsumer { get; set; }

    public bool EnableHandleEventFromInboxBusMessage { get; }

    Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken);

    Task Handle(object notification, CancellationToken cancellationToken);

    bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, object @event);
}

public interface IPlatformCqrsEventApplicationHandler<in TEvent> : IPlatformCqrsEventApplicationHandler, IPlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken);

    bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, TEvent @event);
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
    public virtual bool EnableHandleEventFromInboxBusMessage => true;

    public bool IsCurrentInstanceCalledFromInboxBusMessageConsumer { get; set; }

    public Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken)
    {
        return DoExecuteHandleAsync(@event.As<TEvent>(), cancellationToken);
    }

    public Task Handle(object notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification.As<TEvent>(), cancellationToken);
    }

    public bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, object @event)
    {
        return CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, @event.As<TEvent>());
    }

    public override Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification, cancellationToken);
    }

    public override async Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        await DoExecuteHandleAsync(@event, cancellationToken);
    }

    public virtual bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, TEvent @event)
    {
        // EventHandler using IPlatformApplicationUserContextAccessor cannot use inbox because user request context is not available when process inbox message
        var usingApplicationUserContextAccessor = GetType()
            .GetConstructors()
            .Any(p => p.IsPublic && p.GetParameters().Any(p => p.ParameterType.IsAssignableTo(typeof(IPlatformApplicationUserContextAccessor))));
        var hasEnabledInboxFeature = EnableHandleEventFromInboxBusMessage && hasInboxMessageRepository;

        if (usingApplicationUserContextAccessor && hasEnabledInboxFeature)
            CreateLogger(LoggerFactory)
                .LogError(
                    "[WARNING] Auto handing event directly, not support using InboxEvent. " +
                    "EventHandler using IPlatformApplicationUserContextAccessor cannot use inbox because user request context is not available when process inbox message. " +
                    "Should refactor removing using IPlatformApplicationUserContextAccessor to support inbox.");

        return hasEnabledInboxFeature && !usingApplicationUserContextAccessor;
    }

    private Task DoHandle(TEvent notification, CancellationToken cancellationToken)
    {
        return base.Handle(notification, cancellationToken);
    }

    private async Task DoExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        var hasInboxMessageRepository = PlatformGlobal.RootServiceProvider
            .ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);

        if (CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, @event) && !IsCurrentInstanceCalledFromInboxBusMessageConsumer)
        {
            // Need to get outside of ExecuteInjectScopedAsync to has data because it read current context by thread. If inside => other threads => lose identity data
            var currentBusMessageIdentity =
                BuildCurrentBusMessageIdentity(PlatformGlobal.RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>());

            await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                async (
                    PlatformBusMessageIdentity currentBusMessageIdentity,
                    IServiceProvider serviceProvider,
                    IPlatformInboxBusMessageRepository inboxMessageRepository,
                    IPlatformApplicationSettingContext applicationSettingContext) =>
                {
                    var consumerInstance = serviceProvider.GetRequiredService<PlatformCqrsEventInboxBusMessageConsumer>();

                    await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                        serviceProvider: serviceProvider,
                        consumer: consumerInstance,
                        inboxBusMessageRepository: inboxMessageRepository,
                        message: CqrsEventInboxBusMessage(@event, eventHandlerType: GetType(), applicationSettingContext, currentBusMessageIdentity),
                        routingKey: PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(typeof(TEvent), applicationSettingContext.ApplicationName),
                        loggerFactory: CreateGlobalLogger,
                        retryProcessFailedMessageInSecondsUnit: PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                        cancellationToken: cancellationToken);
                },
                currentBusMessageIdentity);
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
        PlatformBusMessageIdentity currentBusMessageIdentity)
    {
        return PlatformBusMessage<PlatformCqrsEventBusMessagePayload>.New<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>(
            trackId: $"{@event.Id}-{eventHandlerType.Name}",
            payload: PlatformCqrsEventBusMessagePayload.New(@event, eventHandlerType.FullName),
            identity: currentBusMessageIdentity,
            producerContext: applicationSettingContext.ApplicationName,
            messageGroup: nameof(PlatformCqrsEvent),
            messageAction: @event.EventAction);
    }

    public virtual PlatformBusMessageIdentity BuildCurrentBusMessageIdentity(IPlatformApplicationUserContextAccessor userContextAccessor)
    {
        return new PlatformBusMessageIdentity
        {
            UserId = userContextAccessor.Current.UserId(),
            RequestId = userContextAccessor.Current.RequestId(),
            UserName = userContextAccessor.Current.UserName()
        };
    }

    public static ILogger CreateGlobalLogger()
    {
        return CreateLogger(PlatformGlobal.LoggerFactory);
    }
}
