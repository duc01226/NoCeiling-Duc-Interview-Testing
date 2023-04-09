using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.Entities;
using static Easy.Platform.Domain.Entities.ISupportDomainEventsEntity;

namespace Easy.Platform.Domain.Events;

public abstract class PlatformCqrsEntityEvent : PlatformCqrsEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsEntityEvent);
}

/// <summary>
/// This is class of events which is dispatched when an entity is created/updated/deleted.
/// Implement and <see cref="Application.Cqrs.PlatformCqrsEventApplicationHandler{TEvent}" /> to handle any events.
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
}

public enum PlatformCqrsEntityEventCrudAction
{
    Created,
    Updated,
    Deleted
}
