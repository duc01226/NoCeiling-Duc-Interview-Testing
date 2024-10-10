using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Validations.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events.InboxSupport;

public class PlatformCqrsEventInboxBusMessageConsumer : PlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>
{
    public PlatformCqrsEventInboxBusMessageConsumer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(
        loggerFactory,
        uowManager,
        serviceProvider,
        rootServiceProvider)
    {
    }

    public override async Task HandleLogicAsync(PlatformBusMessage<PlatformCqrsEventBusMessagePayload> message, string routingKey)
    {
        await ServiceProvider.ExecuteInjectScopedAsync(
            async (IServiceProvider serviceProvider) =>
            {
                var eventHandlerInstanceResult = RootServiceProvider.GetRegisteredPlatformModuleAssembliesType(message.Payload.EventHandlerTypeFullName)
                    .Validate(must: p => p != null, $"Not found defined event handler. EventHandlerType:{message.Payload.EventHandlerTypeFullName}")
                    .And(
                        must: p => p.FindMatchedGenericType(typeof(IPlatformCqrsEventApplicationHandler<>)) != null,
                        $"Handler {message.Payload.EventHandlerTypeFullName} must extended from {typeof(IPlatformCqrsEventApplicationHandler<>).FullName}")
                    .Then(
                        p => p.Pipe(serviceProvider.GetRequiredService)
                            .As<IPlatformCqrsEventApplicationHandler>()
                            .With(p => p.ThrowExceptionOnHandleFailed = true)
                            .With(p => p.ForceCurrentInstanceHandleInCurrentThread = true)
                            .With(p => p.IsCurrentInstanceCalledFromInboxBusMessageConsumer = true)
                            .With(p => p.RetryOnFailedTimes = 0));
                var eventInstanceResult = RootServiceProvider.GetRegisteredPlatformModuleAssembliesType(message.Payload.EventTypeFullName)
                    .Validate(
                        must: p => p != null,
                        $"[{nameof(PlatformCqrsEventInboxBusMessageConsumer)}] Not found [EventType:{message.Payload.EventTypeFullName}] in application to serialize the message.")
                    .Then(eventType => PlatformJsonSerializer.Deserialize(message.Payload.EventJson, eventType));

                if (eventHandlerInstanceResult.IsValid && eventInstanceResult.IsValid)
                {
                    if (eventHandlerInstanceResult.Value.CanExecuteHandlingEventUsingInboxConsumer(eventInstanceResult.Value) &&
                        await eventHandlerInstanceResult.Value.HandleWhen(eventInstanceResult.Value))
                        await eventHandlerInstanceResult.Value.Handle(eventInstanceResult.Value, CancellationToken.None);
                }
                else
                {
                    HasErrorAndShouldNeverRetry = true;

                    eventHandlerInstanceResult.PipeActionIf(p => !p.IsValid, p => throw new Exception(p.ErrorsMsg()));
                    eventInstanceResult.PipeActionIf(p => !p.IsValid, p => throw new Exception(p.ErrorsMsg()));
                }
            });
    }
}

public class PlatformCqrsEventBusMessagePayload : IPlatformSubMessageQueuePrefixSupport
{
    public string EventJson { get; set; }
    public string EventTypeFullName { get; set; }
    public string EventTypeName { get; set; }
    public string EventHandlerTypeFullName { get; set; }
    public string? SubQueueByIdExtendedPrefixValue { get; set; }

    public string SubQueuePrefix()
    {
        return SubQueueByIdExtendedPrefixValue;
    }

    public static PlatformCqrsEventBusMessagePayload New<TEvent>(TEvent @event, string eventHandlerTypeFullName)
        where TEvent : PlatformCqrsEvent, new()
    {
        return new PlatformCqrsEventBusMessagePayload
        {
            EventJson = @event.ToJson(),
            EventTypeFullName = @event.GetType().FullName,
            EventTypeName = @event.GetType().Name,
            EventHandlerTypeFullName = eventHandlerTypeFullName,
            SubQueueByIdExtendedPrefixValue = @event.As<IPlatformSubMessageQueuePrefixSupport>()?.SubQueuePrefix()
        };
    }
}
