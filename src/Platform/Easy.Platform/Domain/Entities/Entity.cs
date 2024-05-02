using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Validations;
using Easy.Platform.Common.Validations.Validators;

namespace Easy.Platform.Domain.Entities;

/// <summary>
/// This interface is used for conventional type scan for entities.
/// </summary>
public interface IEntity
{
}

/// <summary>
/// Represents an entity with a generic primary key.
/// </summary>
/// <typeparam name="TPrimaryKey">Type of the primary key.</typeparam>
public interface IEntity<TPrimaryKey> : IEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    TPrimaryKey Id { get; set; }
}

/// <summary>
/// Represents an entity that supports validation.
/// </summary>
public interface IValidatableEntity
{
    /// <summary>
    /// Validates the entity and returns the validation result.
    /// </summary>
    /// <returns>Validation result.</returns>
    /// <remarks>
    /// The Easy.Platform.Domain.Entities.IValidatableEntity.Validate method is a part of the IValidatableEntity interface, which is implemented by entities that require validation. This method is used to validate the state of an entity and return a PlatformValidationResult object.
    /// <br />
    /// The PlatformValidationResult object encapsulates the result of the validation process. It contains information about any validation errors that occurred during the validation of the entity. This allows the system to provide detailed feedback about what went wrong during the validation process.
    /// <br />
    /// Entities that implement the IValidatableEntity interface, override the Validate method to provide their own specific validation logic.
    /// <br />
    /// In the IPlatformDbContext implementation class, the EnsureEntityValid method uses the Validate method to ensure that all modified or added entities are valid. If any entity is not valid, an exception is thrown.
    /// <br />
    /// In summary, the Validate method is a key part of the system's validation infrastructure, allowing for the validation of entities and the collection of detailed error information when validation fails.
    /// </remarks>
    PlatformValidationResult Validate();
}

/// <summary>
/// Represents a generic entity that supports validation.
/// </summary>
/// <typeparam name="TEntity">Type of the entity.</typeparam>
public interface IValidatableEntity<TEntity> : IValidatableEntity
{
    /// <inheritdoc cref="IValidatableEntity.Validate" />
    new PlatformValidationResult<TEntity> Validate();
}

/// <summary>
/// Represents an entity that supports domain events.
/// </summary>
public interface ISupportDomainEventsEntity
{
    /// <summary>
    /// Gets the domain events associated with the entity.
    /// </summary>
    /// <returns>List of domain events.</returns>
    /// <remarks>
    /// The GetDomainEvents method is part of the ISupportDomainEventsEntity interface, which represents an entity that supports domain events in the context of Domain-Driven Design (DDD).
    /// <br />
    /// In DDD, a domain event is something that happened in the domain that you want to communicate system-wide. They are used to announce a significant change in the state of the system, which other parts of the system may need to react to.
    /// <br />
    /// The GetDomainEvents method is used to retrieve a list of domain events associated with the entity. Each event is represented as a KeyValuePair where the key is a string (possibly representing the event name or type) and the value is an instance of DomainEvent (or a derived class).
    /// <br />
    /// This method is essential for any entity that needs to communicate changes in its state to other parts of the system. For example, it can be used to trigger specific workflows or update other entities based on the events that occurred.
    /// <br />
    /// In the provided code, we can see that GetDomainEvents is used in several places, such as in the PlatformCqrsEntityEvent and PlatformCqrsBulkEntitiesEvent classes to serialize domain events for auditing or processing, and in the SupportDomainEventsEntityExtensions class to find specific types of domain events associated with an entity.
    /// </remarks>
    public List<KeyValuePair<string, DomainEvent>> GetDomainEvents();

    /// <summary>
    /// Adds a domain event to the entity.
    /// </summary>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <param name="domainEvent">Domain event instance.</param>
    /// <param name="customDomainEventName">Custom domain event name.</param>
    /// <returns>Current instance of <see cref="ISupportDomainEventsEntity" />.</returns>
    public ISupportDomainEventsEntity AddDomainEvent<TEvent>(TEvent domainEvent, string customDomainEventName = null) where TEvent : DomainEvent;

    /// <summary>
    /// Represents a base class for domain events.
    /// </summary>
    public abstract class DomainEvent
    {
        /// <summary>
        /// Gets the default event name for a domain event type.
        /// </summary>
        /// <typeparam name="TEvent">Type of the domain event.</typeparam>
        /// <returns>Default event name.</returns>
        public static string GetDefaultEventName<TEvent>() where TEvent : DomainEvent
        {
            return typeof(TEvent).Name;
        }
    }

    /// <summary>
    /// Represents a domain event for field updates.
    /// </summary>
    public class FieldUpdatedDomainEvent : DomainEvent
    {
        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Gets or sets the original value of the field.
        /// </summary>
        public object OriginalValue { get; set; }

        /// <summary>
        /// Gets or sets the new value of the field.
        /// </summary>
        public object NewValue { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="FieldUpdatedDomainEvent" />.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="originalValue">Original value of the property.</param>
        /// <param name="newValue">New value of the property.</param>
        /// <returns>New instance of <see cref="FieldUpdatedDomainEvent" />.</returns>
        public static FieldUpdatedDomainEvent Create(string propertyName, object originalValue, object newValue)
        {
            return new FieldUpdatedDomainEvent
            {
                FieldName = propertyName,
                OriginalValue = originalValue,
                NewValue = newValue
            };
        }
    }

    /// <summary>
    /// Represents a generic domain event for field updates.
    /// </summary>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    public class FieldUpdatedDomainEvent<TValue> : FieldUpdatedDomainEvent
    {
        /// <summary>
        /// Gets or sets the original value of the field.
        /// </summary>
        public new TValue OriginalValue { get; set; }

        /// <summary>
        /// Gets or sets the new value of the field.
        /// </summary>
        public new TValue NewValue { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="FieldUpdatedDomainEvent{TValue}" />.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="originalValue">Original value of the property.</param>
        /// <param name="newValue">New value of the property.</param>
        /// <returns>New instance of <see cref="FieldUpdatedDomainEvent{TValue}" />.</returns>
        public static FieldUpdatedDomainEvent<TValue> Create(string propertyName, TValue originalValue, TValue newValue)
        {
            return new FieldUpdatedDomainEvent<TValue>
            {
                FieldName = propertyName,
                OriginalValue = originalValue,
                NewValue = newValue
            };
        }
    }
}

/// <summary>
/// Represents a generic entity that supports domain events.
/// </summary>
/// <typeparam name="TEntity">Type of the entity.</typeparam>
public interface ISupportDomainEventsEntity<out TEntity> : ISupportDomainEventsEntity
    where TEntity : class, IEntity, new()
{
    /// <summary>
    /// Adds a domain event to the entity.
    /// </summary>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <param name="eventActionPayload">Domain event instance.</param>
    /// <param name="customDomainEventName">Custom domain event name.</param>
    /// <returns>Current instance of <typeparamref name="TEntity" />.</returns>
    public new TEntity AddDomainEvent<TEvent>(TEvent eventActionPayload, string customDomainEventName = null) where TEvent : DomainEvent;
}

/// <summary>
/// Represents a generic entity that supports validation and has a generic primary key.
/// </summary>
/// <typeparam name="TEntity">Type of the entity.</typeparam>
/// <typeparam name="TPrimaryKey">Type of the primary key.</typeparam>
public interface IValidatableEntity<TEntity, TPrimaryKey> : IValidatableEntity<TEntity>, IEntity<TPrimaryKey>
    where TEntity : IEntity<TPrimaryKey>
{
    /// <summary>
    /// Gets a validator for checking uniqueness during entity creation or update.
    /// </summary>
    /// <returns>PlatformCheckUniqueValidator for <typeparamref name="TEntity" />.</returns>
    public PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator();
}

public interface IUniqueCompositeIdSupport<TEntity>
    where TEntity : class, IEntity, new()
{
    /// <summary>
    /// Gets an expression for finding an entity by its unique composite ID.
    /// Default should return null if no unique composite ID is defined.
    /// Used to check existing for create of update
    /// </summary>
    public Expression<Func<TEntity, bool>> FindByUniqueCompositeIdExpr();


    /// <summary>
    /// Its unique composite ID.
    /// Default should return Null if no unique composite ID is defined.
    /// </summary>
    public string UniqueCompositeId();
}

/// <summary>
/// Represents an abstract class for generic entities that support validation and domain events.
/// </summary>
/// <typeparam name="TEntity">Type of the entity.</typeparam>
/// <typeparam name="TPrimaryKey">Type of the primary key.</typeparam>
public abstract class Entity<TEntity, TPrimaryKey>
    : IValidatableEntity<TEntity, TPrimaryKey>, ISupportDomainEventsEntity<TEntity>, IUniqueCompositeIdSupport<TEntity>
    where TEntity : class, IEntity<TPrimaryKey>, ISupportDomainEventsEntity<TEntity>, IUniqueCompositeIdSupport<TEntity>, new()
{
    /// <summary>
    /// List to store domain events associated with the entity.
    /// </summary>
    protected readonly List<KeyValuePair<string, ISupportDomainEventsEntity.DomainEvent>> DomainEvents = [];

    /// <summary>
    /// Gets the domain events associated with the entity.
    /// </summary>
    /// <returns>List of domain events.</returns>
    public List<KeyValuePair<string, ISupportDomainEventsEntity.DomainEvent>> GetDomainEvents()
    {
        return DomainEvents;
    }

    /// <summary>
    /// Adds a domain event to the entity.
    /// </summary>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <param name="domainEvent">Domain event instance.</param>
    /// <param name="customDomainEventName">Custom domain event name.</param>
    /// <returns>Current instance of <typeparamref name="TEntity" />.</returns>
    ISupportDomainEventsEntity ISupportDomainEventsEntity.AddDomainEvent<TEvent>(TEvent domainEvent, string customDomainEventName)
    {
        return AddDomainEvent(domainEvent, customDomainEventName);
    }

    /// <summary>
    /// Adds a domain event to the entity.
    /// </summary>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <param name="eventActionPayload">Domain event instance.</param>
    /// <param name="customDomainEventName">Custom domain event name.</param>
    /// <returns>Current instance of <typeparamref name="TEntity" />.</returns>
    public virtual TEntity AddDomainEvent<TEvent>(TEvent eventActionPayload, string customDomainEventName = null)
        where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        DomainEvents.Add(
            new KeyValuePair<string, ISupportDomainEventsEntity.DomainEvent>(
                customDomainEventName ?? ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<TEvent>(),
                eventActionPayload));
        return this.As<TEntity>();
    }

    public virtual Expression<Func<TEntity, bool>> FindByUniqueCompositeIdExpr()
    {
        return null;
    }

    public virtual string UniqueCompositeId()
    {
        return null;
    }

    /// <summary>
    /// Gets or sets the primary key of the entity.
    /// </summary>
    public virtual TPrimaryKey Id { get; set; }

    /// <summary>
    /// Gets a validator for checking uniqueness during entity creation or update.
    /// </summary>
    /// <returns>PlatformCheckUniqueValidator for <typeparamref name="TEntity" />.</returns>
    public virtual PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator()
    {
        return null;
    }

    /// <summary>
    /// Validates the entity and returns the validation result.
    /// </summary>
    /// <returns>Validation result.</returns>
    public virtual PlatformValidationResult<TEntity> Validate()
    {
        var validator = GetValidator();
        return validator != null ? validator.Validate(this.As<TEntity>()) : PlatformValidationResult.Valid(this.As<TEntity>());
    }

    /// <summary>
    /// Validates the entity and returns the validation result.
    /// </summary>
    /// <returns>Validation result.</returns>
    PlatformValidationResult IValidatableEntity.Validate()
    {
        return Validate();
    }

    /// <summary>
    /// Adds a field updated domain event to the entity.
    /// </summary>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="propertyName">Name of the property.</param>
    /// <param name="originalValue">Original value of the property.</param>
    /// <param name="newValue">New value of the property.</param>
    /// <returns>Current instance of <typeparamref name="TEntity" />.</returns>
    public TEntity AddFieldUpdatedEvent<TValue>(string propertyName, TValue originalValue, TValue newValue)
    {
        return this.As<TEntity>().AddFieldUpdatedEvent(propertyName, originalValue, newValue);
    }

    /// <summary>
    /// Adds a field updated domain event to the entity using an expression for the property.
    /// </summary>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="property">Expression for the property.</param>
    /// <param name="originalValue">Original value of the property.</param>
    /// <param name="newValue">New value of the property.</param>
    /// <returns>Current instance of <typeparamref name="TEntity" />.</returns>
    public TEntity AddFieldUpdatedEvent<TValue>(Expression<Func<TEntity, TValue>> property, TValue originalValue, TValue newValue)
    {
        return this.As<TEntity>().AddFieldUpdatedEvent(property, originalValue, newValue);
    }

    /// <summary>
    /// Finds domain events of a specific type associated with the entity.
    /// </summary>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <returns>List of domain events of the specified type.</returns>
    public List<TEvent> FindDomainEvents<TEvent>()
        where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        return this.As<TEntity>().FindDomainEvents<TEntity, TEvent>();
    }

    /// <summary>
    /// Finds field updated domain events for a specific field.
    /// </summary>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns>List of field updated domain events for the specified field.</returns>
    public List<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>> FindFieldUpdatedDomainEvents<TValue>(string propertyName)
    {
        return this.As<TEntity>().FindFieldUpdatedDomainEvents<TEntity, TValue>(propertyName);
    }

    /// <summary>
    /// Creates a clone of the entity.
    /// </summary>
    /// <returns>Clone of the entity.</returns>
    public virtual TEntity Clone()
    {
        // doNotTryUseRuntimeType = true to Serialize normally not using the runtime type to prevent error.
        // If using runtime type, the EF Core entity lazy loading proxies will be the runtime type => lead to error
        return PlatformJsonSerializer.Deserialize<TEntity>(PlatformJsonSerializer.Serialize(this.As<TEntity>()));
    }

    /// <summary>
    /// Gets the validator for the entity.
    /// </summary>
    /// <returns>PlatformValidator for <typeparamref name="TEntity" />.</returns>
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
    where TEntity : Entity<TEntity, TPrimaryKey>, IUniqueCompositeIdSupport<TEntity>, new()
{
}
