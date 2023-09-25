using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Exceptions.Extensions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events.InboxSupport;

public class PlatformCqrsEventInboxBusMessageConsumer : PlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>
{
    public PlatformCqrsEventInboxBusMessageConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
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

                var eventHandlerInstance = serviceProvider.GetRequiredService(eventHandlerType)
                    .As<IPlatformCqrsEventApplicationHandler>()
                    .With(_ => _.IsCurrentInstanceCalledFromInboxBusMessageConsumer = true)
                    .With(_ => _.ForceCurrentInstanceHandleInCurrentThread = true);

                var @event = scanAssemblies
                    .Select(p => p.GetType(message.Payload.EventTypeFullName))
                    .FirstOrDefault(p => p != null)
                    .EnsureFound(
                        $"[{nameof(PlatformCqrsEventInboxBusMessageConsumer)}] Not found [EventType:{message.Payload.EventTypeFullName}] in application to serialize the message.")
                    .Pipe(eventType => PlatformJsonSerializer.Deserialize(message.Payload.EventJson, eventType));

                if (eventHandlerInstance.CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageSupport: true, @event))
                    await eventHandlerInstance.Handle(@event, CancellationToken.None);
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
