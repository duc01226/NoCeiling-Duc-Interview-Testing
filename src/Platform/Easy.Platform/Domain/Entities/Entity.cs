using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Validations;
using Easy.Platform.Common.Validations.Validators;
using static Easy.Platform.Domain.Entities.ISupportDomainEventsEntity;

namespace Easy.Platform.Domain.Entities;

/// <summary>
/// This interface is used for conventional type scan for entity
/// </summary>
public interface IEntity
{
}

public interface IEntity<TPrimaryKey> : IEntity
{
    public TPrimaryKey Id { get; set; }
}

public interface IValidatableEntity
{
    public PlatformValidationResult Validate();
}

public interface IValidatableEntity<TEntity> : IValidatableEntity
{
    public new PlatformValidationResult<TEntity> Validate();
}

public interface ISupportDomainEventsEntity
{
    /// <summary>
    /// DomainEvents is used to give more detail about the domain event action inside entity.<br />
    /// It is a list of DomainEventTypeName-DomainEventAsJson from entity domain events
    /// </summary>
    public List<KeyValuePair<string, DomainEvent>> GetDomainEvents();

    public ISupportDomainEventsEntity AddDomainEvent<TEvent>(TEvent domainEvent, string customDomainEventName = null) where TEvent : DomainEvent;

    public abstract class DomainEvent
    {
        public static string GetDefaultDomainEventName<TEvent>() where TEvent : DomainEvent
        {
            return typeof(TEvent).Name;
        }
    }

    public class PropertyValueUpdatedDomainEvent : DomainEvent
    {
        public string PropertyName { get; set; }
        public object OriginalValue { get; set; }
        public object NewValue { get; set; }

        public static PropertyValueUpdatedDomainEvent Create(string propertyName, object originalValue, object newValue)
        {
            return new PropertyValueUpdatedDomainEvent
            {
                PropertyName = propertyName,
                OriginalValue = originalValue,
                NewValue = newValue
            };
        }
    }

    public class PropertyValueUpdatedDomainEvent<TValue> : PropertyValueUpdatedDomainEvent
    {
        public new TValue OriginalValue { get; set; }
        public new TValue NewValue { get; set; }

        public static PropertyValueUpdatedDomainEvent<TValue> Create(string propertyName, TValue originalValue, TValue newValue)
        {
            return new PropertyValueUpdatedDomainEvent<TValue>
            {
                PropertyName = propertyName,
                OriginalValue = originalValue,
                NewValue = newValue
            };
        }
    }
}

public interface ISupportDomainEventsEntity<out TEntity> : ISupportDomainEventsEntity
    where TEntity : class, IEntity, new()
{
    public new TEntity AddDomainEvent<TEvent>(TEvent eventActionPayload, string customDomainEventName = null) where TEvent : DomainEvent;
}

/// <summary>
/// Ensure concurrent update is not conflicted
/// </summary>
public interface IRowVersionEntity : IEntity
{
    /// <summary>
    /// This is used as a Concurrency Token to track entity version to prevent concurrent update
    /// </summary>
    public Guid? ConcurrencyUpdateToken { get; set; }
}

public interface IValidatableEntity<TEntity, TPrimaryKey> : IValidatableEntity<TEntity>, IEntity<TPrimaryKey>
    where TEntity : IEntity<TPrimaryKey>
{
    /// <summary>
    /// Default return null. Default check unique is by Id. <br />
    /// If return not null, this will be used instead to check the entity is unique to create or update
    /// </summary>
    public PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator();
}

public abstract class Entity<TEntity, TPrimaryKey> : IValidatableEntity<TEntity, TPrimaryKey>, ISupportDomainEventsEntity<TEntity>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
    protected readonly List<KeyValuePair<string, DomainEvent>> DomainEvents = new();

    public List<KeyValuePair<string, DomainEvent>> GetDomainEvents()
    {
        return DomainEvents;
    }

    ISupportDomainEventsEntity ISupportDomainEventsEntity.AddDomainEvent<TEvent>(TEvent domainEvent, string customDomainEventName)
    {
        return AddDomainEvent(domainEvent, customDomainEventName);
    }

    public TEntity AddDomainEvent<TEvent>(TEvent eventActionPayload, string customDomainEventName = null)
        where TEvent : DomainEvent
    {
        DomainEvents.Add(new KeyValuePair<string, DomainEvent>(customDomainEventName ?? DomainEvent.GetDefaultDomainEventName<TEvent>(), eventActionPayload));
        return (TEntity)this;
    }


    public TPrimaryKey Id { get; set; }

    public virtual PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator()
    {
        return null;
    }

    public virtual PlatformValidationResult<TEntity> Validate()
    {
        var validator = GetValidator();

        return validator != null ? validator.Validate((TEntity)this) : PlatformValidationResult.Valid((TEntity)this);
    }

    PlatformValidationResult IValidatableEntity.Validate()
    {
        return Validate();
    }

    public TEntity AddPropertyValueUpdatedDomainEvent<TValue>(string propertyName, TValue originalValue, TValue newValue)
    {
        return this.As<TEntity>().AddPropertyValueUpdatedDomainEvent<TEntity, TValue>(propertyName, originalValue, newValue);
    }

    public TEntity AddPropertyValueUpdatedDomainEvent<TValue>(Expression<Func<TEntity, TValue>> property, TValue originalValue, TValue newValue)
    {
        return this.As<TEntity>().AddPropertyValueUpdatedDomainEvent<TEntity, TValue>(property, originalValue, newValue);
    }

    public List<TEvent> FindDomainEvents<TEvent>()
        where TEvent : DomainEvent
    {
        return this.As<TEntity>().FindDomainEvents<TEntity, TEvent>();
    }

    public List<PropertyValueUpdatedDomainEvent<TValue>> FindPropertyValueUpdatedDomainEvents<TValue>(string propertyName)
    {
        return this.As<TEntity>().FindPropertyValueUpdatedDomainEvents<TEntity, TValue>(propertyName);
    }

    public virtual TEntity Clone()
    {
        // doNotTryUseRuntimeType = true to Serialize normally not using the runtime type to prevent error.
        // If using runtime type, the ef core entity lazy loading proxies will be the runtime type => lead to error
        return PlatformJsonSerializer.Deserialize<TEntity>(PlatformJsonSerializer.Serialize(this.As<TEntity>(), doNotTryUseRuntimeType: true));
    }

    /// <summary>
    /// To get the entity validator. <br />
    /// This will help us to centralize and reuse domain validation logic. Ensure any request which update/create domain entity
    /// use the same entity validation logic (Single Responsibility, Don't Repeat YourSelf).
    /// </summary>
    public virtual PlatformValidator<TEntity> GetValidator()
    {
        return null;
    }
}

public interface IRootEntity<TPrimaryKey> : IEntity<TPrimaryKey>
{
}

/// <summary>
/// Root entity represent an aggregate root entity. Only root entity can be Create/Update/Delete via repository
/// </summary>
public abstract class RootEntity<TEntity, TPrimaryKey> : Entity<TEntity, TPrimaryKey>, IRootEntity<TPrimaryKey>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
}
