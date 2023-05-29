using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Domain.Events;

public abstract class PlatformCqrsEntityEvent : PlatformCqrsEvent, IPlatformUowEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsEntityEvent);

    public string SourceUowId { get; set; }

    public static async Task SendEvent<TEntity, TPrimaryKey>(
        IUnitOfWork mappedToDbContextUow,
        TEntity entity,
        PlatformCqrsEntityEventCrudAction crudAction,
        bool hasSupportOutboxEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entityEvent = new PlatformCqrsEntityEvent<TEntity>(entity, crudAction)
            .With(_ => sendEntityEventConfigure?.Invoke(_))
            .With(_ => _.SourceUowId = mappedToDbContextUow.Id);

        await mappedToDbContextUow.CreatedByUnitOfWorkManager.CurrentSameScopeCqrs.SendEvent(entityEvent, cancellationToken);
    }

    public static async Task<TResult> ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey, TResult>(
        IUnitOfWork mappedToDbContextUow,
        TEntity entity,
        Func<TEntity, Task<TResult>> deleteEntityAction,
        bool dismissSendEvent,
        bool hasSupportOutboxEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var result = await deleteEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            mappedToDbContextUow,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Deleted,
                            hasSupportOutboxEvent,
                            sendEntityEventConfigure: sendEntityEventConfigure,
                            cancellationToken);
                });

        return result;
    }

    public static async Task<TResult> ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TResult>(
        IUnitOfWork mappedToDbContextUow,
        TEntity entity,
        Func<TEntity, Task<TResult>> createEntityAction,
        bool dismissSendEvent,
        bool hasSupportOutboxEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var result = await createEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            mappedToDbContextUow,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Created,
                            hasSupportOutboxEvent,
                            sendEntityEventConfigure: sendEntityEventConfigure,
                            cancellationToken);
                });

        return result;
    }

    public static async Task<TResult> ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TResult>(
        IUnitOfWork unitOfWork,
        TEntity entity,
        TEntity existingEntity,
        Func<TEntity, Task<TResult>> updateEntityAction,
        bool dismissSendEvent,
        bool hasSupportOutboxEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (!dismissSendEvent)
            entity.AutoAddPropertyValueUpdatedDomainEvent(existingEntity);

        var result = await updateEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            unitOfWork,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Updated,
                            hasSupportOutboxEvent,
                            sendEntityEventConfigure: sendEntityEventConfigure,
                            cancellationToken);
                });

        return result;
    }

    /// <inheritdoc cref="PlatformCqrsEvent.SetForceWaitEventHandlerFinished" />
    public new virtual PlatformCqrsEntityEvent SetForceWaitEventHandlerFinished<THandler, TEvent>()
        where THandler : IPlatformCqrsEventHandler<TEvent>
        where TEvent : PlatformCqrsEntityEvent, new()
    {
        return SetForceWaitEventHandlerFinished(typeof(THandler)).Cast<PlatformCqrsEntityEvent>();
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

    public List<TEvent> FindDomainEvents<TEvent>() where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        return DomainEvents
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultDomainEventName<TEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<TEvent>(p.Value))
            .ToList();
    }

    public ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue> FindPropertyValueUpdatedDomainEvent<TValue>(
        Expression<Func<TEntity, TValue>> property)
    {
        return DomainEvents
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultDomainEventName<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>>(p.Value))
            .FirstOrDefault(p => p.PropertyName == property.GetPropertyName());
    }

    public PlatformCqrsEntityEvent<TEntity> Clone()
    {
        return MemberwiseClone().As<PlatformCqrsEntityEvent<TEntity>>();
    }

    /// <inheritdoc cref="PlatformCqrsEvent.SetForceWaitEventHandlerFinished{THandler,TEvent}" />
    public virtual PlatformCqrsEntityEvent<TEntity> SetForceWaitEventHandlerFinished<THandler>()
        where THandler : IPlatformCqrsEventHandler<PlatformCqrsEntityEvent<TEntity>>
    {
        return SetForceWaitEventHandlerFinished(typeof(THandler)).Cast<PlatformCqrsEntityEvent<TEntity>>();
    }
}

public enum PlatformCqrsEntityEventCrudAction
{
    Created,
    Updated,
    Deleted
}
