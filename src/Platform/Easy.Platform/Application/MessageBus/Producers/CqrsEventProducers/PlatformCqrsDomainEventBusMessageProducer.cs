using Easy.Platform.Common;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsDomainEventBusMessageProducer<TDomainEvent>
    : PlatformCqrsEventBusMessageProducer<TDomainEvent, PlatformCqrsDomainEventBusMessage<TDomainEvent>>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventBusMessageProducer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider,
        applicationBusMessageProducer)
    {
    }

    protected override PlatformCqrsDomainEventBusMessage<TDomainEvent> BuildMessage(TDomainEvent @event)
    {
        return PlatformCqrsDomainEventBusMessage<TDomainEvent>.New<PlatformCqrsDomainEventBusMessage<TDomainEvent>>(
            trackId: Ulid.NewUlid().ToString(),
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(@event.RequestContext),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsDomainEvent.EventTypeValue,
            messageAction: @event.EventAction,
            requestContext: @event.RequestContext);
    }

    /// <summary>
    /// Default return True
    /// </summary>
    public override bool HandleWhen(TDomainEvent @event)
    {
        return true;
    }
}

public class PlatformCqrsDomainEventBusMessage<TDomainEvent> : PlatformBusMessage<TDomainEvent>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
}
