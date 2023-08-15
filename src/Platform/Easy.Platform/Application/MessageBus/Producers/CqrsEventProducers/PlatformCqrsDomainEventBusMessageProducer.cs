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
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
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
            messageAction: @event.EventAction,
            requestContext: PlatformApplicationGlobal.UserContext.Current.GetAllKeyValues());
    }
}

public class PlatformCqrsDomainEventBusMessage<TDomainEvent> : PlatformBusMessage<TDomainEvent>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
}
