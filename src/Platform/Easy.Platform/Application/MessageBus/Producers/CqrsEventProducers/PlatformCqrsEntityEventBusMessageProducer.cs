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

    protected override bool HandleWhen(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return CheckCanHandleSendMessage(@event).GetResult();
    }

    protected override TMessage BuildMessage(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return PlatformCqrsEntityEventBusMessage<TEntity>.New<TMessage>(
            trackId: @event.Id,
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
        await CheckCanHandleSendMessage(@event, entityEvent => base.SendMessage(entityEvent, cancellationToken));
    }

    private async Task<bool> CheckCanHandleSendMessage(
        PlatformCqrsEntityEvent<TEntity> @event,
        Func<PlatformCqrsEntityEvent<TEntity>, Task> onCanSendMessageFn = null)
    {
        if (SendMessageWhen(@event))
        {
            if (onCanSendMessageFn != null) await onCanSendMessageFn(@event);
            return true;
        }

        if (@event.CrudAction.IsTrackingCrudAction() && UnitOfWorkManager.HasCurrentActiveUow() && ApplicationBusMessageProducer.HasOutboxMessageSupport())
        {
            var mappedToUowCompletedCrudActionEntityEvent = @event.Clone().With(p => p.CrudAction = p.CrudAction.GetRelevantCompletedCrudAction());

            if (SendMessageWhen(mappedToUowCompletedCrudActionEntityEvent))
            {
                if (onCanSendMessageFn != null) await onCanSendMessageFn(mappedToUowCompletedCrudActionEntityEvent);
                return true;
            }
        }

        return false;
    }
}

public class PlatformCqrsEntityEventBusMessage<TEntity> : PlatformBusMessage<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
}
