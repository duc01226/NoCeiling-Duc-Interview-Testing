using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
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
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerBuilder,
        unitOfWorkManager,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    protected override PlatformCqrsDomainEventBusMessage<TDomainEvent> BuildMessage(TDomainEvent @event)
    {
        return PlatformCqrsDomainEventBusMessage<TDomainEvent>.New<PlatformCqrsDomainEventBusMessage<TDomainEvent>>(
            trackId: Guid.NewGuid().ToString(),
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsDomainEvent.EventTypeValue,
            messageAction: @event.EventAction);
    }
}

public class PlatformCqrsDomainEventBusMessage<TDomainEvent> : PlatformBusMessage<TDomainEvent>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
}
