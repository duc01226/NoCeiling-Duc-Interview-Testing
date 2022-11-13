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

    public abstract class DomainEvent
    {
    }
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
    /// Default return null. Default check unique is by Id. </br>
    /// If return not null, this will be used instead to check the entity is unique to create or update
    /// </summary>
    public PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator();
}

public abstract class Entity<TEntity, TPrimaryKey> : IValidatableEntity<TEntity, TPrimaryKey>, ISupportDomainEventsEntity
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
    protected readonly List<KeyValuePair<string, DomainEvent>> DomainEvents = new();

    public List<KeyValuePair<string, DomainEvent>> GetDomainEvents()
    {
        return DomainEvents;
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

    public virtual TEntity Clone()
    {
        return PlatformJsonSerializer.Deserialize<TEntity>(PlatformJsonSerializer.Serialize(this));
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

    protected TEntity AddDomainEvents<TEvent>(TEvent eventActionPayload)
        where TEvent : DomainEvent
    {
        DomainEvents.Add(new KeyValuePair<string, DomainEvent>(typeof(TEvent).Name, eventActionPayload));
        return (TEntity)this;
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
