using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsEntityEventBusMessageProducer<TMessage, TEntity>
    : PlatformCqrsEventBusMessageProducer<PlatformCqrsEntityEvent<TEntity>, TMessage>
    where TEntity : class, IEntity, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
    protected PlatformCqrsEntityEventBusMessageProducer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider,
        applicationBusMessageProducer,
        applicationSettingContext)
    {
    }

    protected override TMessage BuildMessage(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return PlatformCqrsEntityEventBusMessage<TEntity>.New<TMessage>(
            trackId: @event.Id,
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(@event.RequestContext),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsEntityEvent.EventTypeValue,
            messageAction: @event.EventAction,
            requestContext: @event.RequestContext);
    }
}

public class PlatformCqrsEntityEventBusMessage<TEntity> : PlatformBusMessage<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
}
