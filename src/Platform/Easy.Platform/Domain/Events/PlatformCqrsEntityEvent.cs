using System.ComponentModel;
using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using static Easy.Platform.Domain.Entities.ISupportDomainEventsEntity;

namespace Easy.Platform.Domain.Events;

public abstract class PlatformCqrsEntityEvent : PlatformCqrsEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsEntityEvent);

    public static async Task SendEvent<TEntity, TPrimaryKey>(
        IPlatformCqrs cqrs,
        IUnitOfWork? unitOfWork,
        TEntity entity,
        PlatformCqrsEntityEventCrudAction crudAction,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (crudAction.IsCompletedCrudAction() && unitOfWork != null && !unitOfWork.IsPseudoTransactionUow())
            unitOfWork.OnCompleted += (object sender, EventArgs e) =>
            {
                // Do not use async, just call.WaitResult()
                // WHY: Never use async lambda on event handler, because it's equivalent to async void, which fire async task and forget
                // this will lead to a lot of potential bug and issues.
                cqrs.SendEntityEvent(entity, crudAction, cancellationToken).WaitResult();
            };
        else await cqrs.SendEntityEvent(entity, crudAction, cancellationToken);
    }

    public static async Task ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey>(
        IPlatformCqrs cqrs,
        IUnitOfWork? unitOfWork,
        TEntity entity,
        Func<TEntity, Task> deleteEntityAction,
        bool dismissSendEvent,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (!dismissSendEvent)
            await SendEvent<TEntity, TPrimaryKey>(
                cqrs,
                unitOfWork,
                entity,
                PlatformCqrsEntityEventCrudAction.TrackDeleting,
                cancellationToken);

        await deleteEntityAction(entity);

        if (!dismissSendEvent)
            await SendEvent<TEntity, TPrimaryKey>(
                cqrs,
                unitOfWork,
                entity,
                PlatformCqrsEntityEventCrudAction.Deleted,
                cancellationToken);
    }

    public static async Task<TResult> ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TResult>(
        IPlatformCqrs cqrs,
        IUnitOfWork? unitOfWork,
        TEntity entity,
        Func<TEntity, Task<TResult>> createEntityAction,
        bool dismissSendEvent,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (!dismissSendEvent)
            await SendEvent<TEntity, TPrimaryKey>(
                cqrs,
                unitOfWork,
                entity,
                PlatformCqrsEntityEventCrudAction.TrackCreating,
                cancellationToken);

        var result = await createEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            cqrs,
                            unitOfWork,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Created,
                            cancellationToken);
                });

        return result;
    }

    public static async Task<TResult> ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TResult>(
        IPlatformCqrs cqrs,
        IUnitOfWork? unitOfWork,
        TEntity entity,
        TEntity existingEntity,
        Func<TEntity, Task<TResult>> updateEntityAction,
        bool dismissSendEvent,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (!dismissSendEvent)
            entity.AutoAddPropertyValueUpdatedDomainEvent(existingEntity);

        if (!dismissSendEvent)
            await SendEvent<TEntity, TPrimaryKey>(
                cqrs,
                unitOfWork,
                entity,
                PlatformCqrsEntityEventCrudAction.TrackUpdating,
                cancellationToken);

        var result = await updateEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            cqrs,
                            unitOfWork,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Updated,
                            cancellationToken);
                });

        return result;
    }
}

/// <summary>
/// This is class of events which is dispatched when an entity is created/updated/deleted.
/// Implement and <see cref="Application.Cqrs.Events.PlatformCqrsEventApplicationHandler{TEvent}" /> to handle any events.
/// </summary>
public class PlatformCqrsEntityEvent<TEntity> : PlatformCqrsEntityEvent
    where TEntity : class, IEntity, new()
{
    public PlatformCqrsEntityEvent() { }

    public PlatformCqrsEntityEvent(
        TEntity entityData,
        PlatformCqrsEntityEventCrudAction crudAction)
    {
        AuditTrackId = Guid.NewGuid().ToString();
        EntityData = entityData;
        CrudAction = crudAction;

        if (entityData is ISupportDomainEventsEntity businessActionEventsEntity)
            DomainEvents = businessActionEventsEntity.GetDomainEvents()
                .Select(p => new KeyValuePair<string, string>(p.Key, PlatformJsonSerializer.Serialize(p.Value)))
                .ToList();
    }

    public override string EventType => EventTypeValue;
    public override string EventName => typeof(TEntity).Name;
    public override string EventAction => CrudAction.ToString();

    public TEntity EntityData { get; set; }

    public PlatformCqrsEntityEventCrudAction CrudAction { get; set; }

    /// <summary>
    /// DomainEvents is used to give more detail about the domain event action inside entity.<br />
    /// It is a list of DomainEventName-DomainEventAsJson from entity domain events
    /// </summary>
    public List<KeyValuePair<string, string>> DomainEvents { get; set; } = new();

    public List<TEvent> FindDomainEvents<TEvent>() where TEvent : DomainEvent
    {
        return DomainEvents
            .Where(p => p.Key == DomainEvent.GetDefaultDomainEventName<TEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<TEvent>(p.Value))
            .ToList();
    }

    public PropertyValueUpdatedDomainEvent<TValue> FindPropertyValueUpdatedDomainEvent<TValue>(
        Expression<Func<TEntity, TValue>> property)
    {
        return DomainEvents
            .Where(p => p.Key == DomainEvent.GetDefaultDomainEventName<PropertyValueUpdatedDomainEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<PropertyValueUpdatedDomainEvent<TValue>>(p.Value))
            .FirstOrDefault(p => p.PropertyName == property.GetPropertyName());
    }

    public PlatformCqrsEntityEvent<TEntity> Clone()
    {
        return MemberwiseClone().As<PlatformCqrsEntityEvent<TEntity>>();
    }
}

public enum PlatformCqrsEntityEventCrudAction
{
    /// <summary>
    /// Track Creating Before complete current UOW
    /// </summary>
    TrackCreating,

    /// <summary>
    /// Track Updating Before complete current UOW
    /// </summary>
    TrackUpdating,

    /// <summary>
    /// Track Deleting Before complete current UOW
    /// </summary>
    TrackDeleting,

    // After completed UOW
    Created,
    Updated,
    Deleted
}

public static class PlatformCqrsEntityEventCrudActionExtensions
{
    public static bool IsTrackingCrudAction(this PlatformCqrsEntityEventCrudAction crudAction)
    {
        return crudAction == PlatformCqrsEntityEventCrudAction.TrackCreating ||
               crudAction == PlatformCqrsEntityEventCrudAction.TrackUpdating ||
               crudAction == PlatformCqrsEntityEventCrudAction.TrackDeleting;
    }

    public static bool IsCompletedCrudAction(this PlatformCqrsEntityEventCrudAction crudAction)
    {
        return crudAction == PlatformCqrsEntityEventCrudAction.Created ||
               crudAction == PlatformCqrsEntityEventCrudAction.Updated ||
               crudAction == PlatformCqrsEntityEventCrudAction.Deleted;
    }

    public static PlatformCqrsEntityEventCrudAction GetRelevantCompletedCrudAction(this PlatformCqrsEntityEventCrudAction crudAction)
    {
        return crudAction switch
        {
            PlatformCqrsEntityEventCrudAction.TrackCreating => PlatformCqrsEntityEventCrudAction.Created,
            PlatformCqrsEntityEventCrudAction.TrackUpdating => PlatformCqrsEntityEventCrudAction.Updated,
            PlatformCqrsEntityEventCrudAction.TrackDeleting => PlatformCqrsEntityEventCrudAction.Deleted,
            _ => throw new InvalidEnumArgumentException($"Invalid CrudAction value:{crudAction}")
        };
    }

    public static PlatformCqrsEntityEventCrudAction GetRelevantTrackingCrudAction(this PlatformCqrsEntityEventCrudAction crudAction)
    {
        return crudAction switch
        {
            PlatformCqrsEntityEventCrudAction.Created => PlatformCqrsEntityEventCrudAction.TrackCreating,
            PlatformCqrsEntityEventCrudAction.Updated => PlatformCqrsEntityEventCrudAction.TrackUpdating,
            PlatformCqrsEntityEventCrudAction.Deleted => PlatformCqrsEntityEventCrudAction.TrackDeleting,
            _ => throw new InvalidEnumArgumentException($"Invalid CrudAction value:{crudAction}")
        };
    }
}
