using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Domain.Events;

public interface IPlatformCqrsEntityEvent : IPlatformCqrsEvent
{
    string SourceUowId { get; set; }

    /// <inheritdoc cref="PlatformCqrsEvent.SetWaitHandlerExecutionFinishedImmediately" />
    PlatformCqrsEntityEvent SetForceWaitEventHandlerFinished<THandler>()
        where THandler : IPlatformCqrsEventHandler;
}

public abstract class PlatformCqrsEntityEvent : PlatformCqrsEvent, IPlatformUowEvent, IPlatformCqrsEntityEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsEntityEvent);

    /// <inheritdoc cref="PlatformCqrsEvent.SetWaitHandlerExecutionFinishedImmediately" />
    public virtual PlatformCqrsEntityEvent SetForceWaitEventHandlerFinished<THandler>()
        where THandler : IPlatformCqrsEventHandler
    {
        return SetWaitHandlerExecutionFinishedImmediately(typeof(THandler)).Cast<PlatformCqrsEntityEvent>();
    }

    public string SourceUowId { get; set; }

    private static async Task SendEvent<TEvent>(
        IServiceProvider serviceProvider,
        [AllowNull] IUnitOfWork mappedToDbContextUow,
        Func<TEvent> eventBuilder,
        Action<TEvent> eventCustomConfig,
        Func<IDictionary<string, object>> requestContext,
        CancellationToken cancellationToken)
        where TEvent : PlatformCqrsEntityEvent
    {
        var entityEvent = eventBuilder()
            .With(@event => eventCustomConfig?.Invoke(@event))
            .With(@event => @event.SourceUowId = mappedToDbContextUow?.Id)
            .WithIf(requestContext != null, @event => @event.SetRequestContextValues(requestContext!()));

        if (mappedToDbContextUow != null)
            await mappedToDbContextUow.CreatedByUnitOfWorkManager.CurrentSameScopeCqrs.SendEvent(entityEvent, cancellationToken);
        else
            await serviceProvider.ExecuteInjectScopedAsync(
                (IPlatformCqrs cqrs) =>
                {
                    cqrs.SendEvent(entityEvent, cancellationToken);
                });
    }

    public static async Task SendEvent<TEntity, TPrimaryKey>(
        IServiceProvider serviceProvider,
        [AllowNull] IUnitOfWork mappedToDbContextUow,
        TEntity entity,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        Func<IDictionary<string, object>> requestContext,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await SendEvent(
            serviceProvider,
            mappedToDbContextUow,
            () => new PlatformCqrsEntityEvent<TEntity>(entity, crudAction),
            eventCustomConfig,
            requestContext,
            cancellationToken);
    }

    public static async Task SendBulkEntitiesEvent<TEntity, TPrimaryKey>(
        IServiceProvider serviceProvider,
        [AllowNull] IUnitOfWork mappedToDbContextUow,
        List<TEntity> entities,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        Func<IDictionary<string, object>> requestContext,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await SendEvent(
            serviceProvider,
            mappedToDbContextUow,
            () => new PlatformCqrsBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, crudAction),
            eventCustomConfig,
            requestContext,
            cancellationToken);
    }

    public static async Task<TResult> ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey, TResult>(
        IServiceProvider serviceProvider,
        IUnitOfWork mappedToDbContextUow,
        TEntity entity,
        Func<TEntity, Task<TResult>> deleteEntityAction,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        Func<IDictionary<string, object>> requestContext,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var result = await deleteEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            serviceProvider,
                            mappedToDbContextUow,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Deleted,
                            eventCustomConfig: eventCustomConfig,
                            requestContext: requestContext,
                            cancellationToken);
                });

        return result;
    }

    public static async Task<TResult> ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TResult>(
        IServiceProvider serviceProvider,
        IUnitOfWork mappedToDbContextUow,
        TEntity entity,
        Func<TEntity, Task<TResult>> createEntityAction,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        Func<IDictionary<string, object>> requestContext,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var result = await createEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            serviceProvider,
                            mappedToDbContextUow,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Created,
                            eventCustomConfig: eventCustomConfig,
                            requestContext: requestContext,
                            cancellationToken);
                });

        return result;
    }

    public static async Task<TResult> ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TResult>(
        IServiceProvider serviceProvider,
        IUnitOfWork unitOfWork,
        TEntity entity,
        TEntity existingEntity,
        Func<TEntity, Task<TResult>> updateEntityAction,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        Func<IDictionary<string, object>> requestContext,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (!dismissSendEvent)
            entity.AutoAddFieldUpdatedEvent(existingEntity);

        var result = await updateEntityAction(entity)
            .ThenActionAsync(
                async _ =>
                {
                    if (!dismissSendEvent)
                        await SendEvent<TEntity, TPrimaryKey>(
                            serviceProvider,
                            unitOfWork,
                            entity,
                            PlatformCqrsEntityEventCrudAction.Updated,
                            eventCustomConfig: eventCustomConfig,
                            requestContext: requestContext,
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

    public List<TEvent> FindEvents<TEvent>() where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        return DomainEvents
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<TEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<TEvent>(p.Value))
            .ToList();
    }

    public ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue> FindFieldUpdatedEvent<TValue>(
        Expression<Func<TEntity, TValue>> field)
    {
        return DomainEvents
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>>(p.Value))
            .FirstOrDefault(p => p != null && p.FieldName == field.GetPropertyName());
    }

    public bool HasAnyFieldUpdated(
        params Expression<Func<TEntity, object>>[] fields)
    {
        var fieldNames = fields.Select(p => p.GetPropertyName()).ToHashSet();

        return DomainEvents
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<object>>(p.Value))
            .Any(p => p != null && fieldNames.Contains(p.FieldName));
    }

    public PlatformCqrsEntityEvent<TEntity> Clone()
    {
        return MemberwiseClone().As<PlatformCqrsEntityEvent<TEntity>>();
    }
}

/// <summary>
/// This is class of events which is dispatched when list of entities is createdMany/updatedMany/deletedMany.
/// Implement and <see cref="Application.Cqrs.Events.PlatformCqrsEventApplicationHandler{TEvent}" /> to handle any events.
/// </summary>
public class PlatformCqrsBulkEntitiesEvent<TEntity, TPrimaryKey> : PlatformCqrsEntityEvent
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    public PlatformCqrsBulkEntitiesEvent() { }

    public PlatformCqrsBulkEntitiesEvent(
        IList<TEntity> entities,
        PlatformCqrsEntityEventCrudAction crudAction)
    {
        AuditTrackId = Guid.NewGuid().ToString();
        Entities = entities;
        CrudAction = crudAction;

        if (typeof(TEntity).IsAssignableTo(typeof(ISupportDomainEventsEntity)))
            DomainEvents = entities.ToDictionary(
                entity => entity.Id,
                entity => entity.As<ISupportDomainEventsEntity>().GetDomainEvents()
                    .Select(p => new KeyValuePair<string, string>(p.Key, PlatformJsonSerializer.Serialize(p.Value)))
                    .ToList());
    }

    public override string EventType => EventTypeValue;
    public override string EventName => typeof(TEntity).Name;
    public override string EventAction => CrudAction.ToString();

    public IList<TEntity> Entities { get; set; }

    public PlatformCqrsEntityEventCrudAction CrudAction { get; set; }

    /// <summary>
    /// DomainEvents is used to give more detail about the domain event action inside entity.<br />
    /// It is a dictionary of EntityId => list of DomainEventName-DomainEventAsJson from entity domain events
    /// </summary>
    public Dictionary<TPrimaryKey, List<KeyValuePair<string, string>>> DomainEvents { get; set; } = new();

    public List<TEvent> FindEvents<TEvent>(TPrimaryKey entityId) where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        return DomainEvents[entityId]
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<TEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<TEvent>(p.Value))
            .ToList();
    }

    public ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue> FindFieldUpdatedEvent<TValue>(
        TPrimaryKey entityId,
        Expression<Func<TEntity, TValue>> field)
    {
        return DomainEvents[entityId]
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>>(p.Value))
            .FirstOrDefault(p => p != null && p.FieldName == field.GetPropertyName());
    }

    public bool HasAnyFieldUpdated(
        TPrimaryKey entityId,
        params Expression<Func<TEntity, object>>[] fields)
    {
        var fieldNames = fields.Select(p => p.GetPropertyName()).ToHashSet();

        return DomainEvents[entityId]
            .Where(p => p.Key == ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>())
            .Select(p => PlatformJsonSerializer.TryDeserializeOrDefault<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<object>>(p.Value))
            .Any(p => p != null && fieldNames.Contains(p.FieldName));
    }

    public PlatformCqrsEntityEvent<TEntity> Clone()
    {
        return MemberwiseClone().As<PlatformCqrsEntityEvent<TEntity>>();
    }
}

public enum PlatformCqrsEntityEventCrudAction
{
    Created,
    Updated,
    Deleted
}
