using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Extensions;
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
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    protected override TMessage BuildMessage(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return PlatformCqrsEntityEventBusMessage<TEntity>.New<TMessage>(
            trackId: Guid.NewGuid().ToString(),
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsEntityEvent.EventTypeValue,
            messageAction: @event.EventAction);
    }

    /// <summary>
    /// Override to define when to send message for an event. Return true will send the message
    /// </summary>
    protected virtual bool SendMessageWhen(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return @event.CrudAction switch
        {
            PlatformCqrsEntityEventCrudAction.Created => true,
            PlatformCqrsEntityEventCrudAction.Updated => true,
            PlatformCqrsEntityEventCrudAction.Deleted => true,
            _ => false
        };
    }

    protected override async Task SendMessage(PlatformCqrsEntityEvent<TEntity> @event, CancellationToken cancellationToken)
    {
        if (SendMessageWhen(@event))
        {
            // Do not need to send message again if HasOutboxMessageSupport and crud action is IsAfterCompletedUowEntityEvent
            // because of out box support, we will register the outbox message already before complete uow, with crud action value is mapped from before to after CompletedUowCrudAction
            // using MapFromBeforeToAfterCompletedUowCrudAction
            if (!@event.CrudAction.IsCompletedCrudAction() || !ApplicationBusMessageProducer.HasOutboxMessageSupport())
                await base.SendMessage(@event, cancellationToken);
        }
        else
        {
            if (@event.CrudAction.IsTrackingCrudAction() &&
                ApplicationBusMessageProducer.HasOutboxMessageSupport())
            {
                var mappedToUowCompletedCrudActionEntityEvent = @event.Clone().With(p => p.CrudAction = p.CrudAction.GetRelevantCompletedCrudAction());

                if (SendMessageWhen(mappedToUowCompletedCrudActionEntityEvent))
                    await base.SendMessage(mappedToUowCompletedCrudActionEntityEvent, cancellationToken);
            }
        }
    }
}

public class PlatformCqrsEntityEventBusMessage<TEntity> : PlatformBusMessage<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
}
