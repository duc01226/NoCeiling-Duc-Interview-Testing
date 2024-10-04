using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsEntityEventBusMessageProducer<TMessage, TEntity, TPrimaryKey>
    : PlatformCqrsEventBusMessageProducer<PlatformCqrsEntityEvent<TEntity>, TMessage>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
    protected PlatformCqrsEntityEventBusMessageProducer(
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

    protected override TMessage BuildMessage(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return PlatformCqrsEntityEventBusMessage<TEntity, TPrimaryKey>.New<TMessage>(
            trackId: @event.Id,
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(@event.RequestContext),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsEntityEvent.EventTypeValue,
            messageAction: @event.EventAction,
            requestContext: @event.RequestContext);
    }

    /// <summary>
    /// Default handle only when data has changed
    /// </summary>
    public override async Task<bool> HandleWhen(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return @event.CrudAction == PlatformCqrsEntityEventCrudAction.Created ||
               @event.CrudAction == PlatformCqrsEntityEventCrudAction.Deleted ||
               @event.ExistingEntityData == null ||
               @event.ExistingEntityData.ToJson() != @event.EntityData.ToJson();
    }
}

public class PlatformCqrsEntityEventBusMessage<TEntity, TPrimaryKey> : PlatformBusMessage<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    public override string SubQueuePrefix()
    {
        return Payload?.EntityData?.Id?.ToString() ?? TrackingId;
    }
}
