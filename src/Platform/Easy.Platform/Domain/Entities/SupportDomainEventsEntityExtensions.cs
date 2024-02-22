using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.Entities;

/// <summary>
/// Extension methods for entities that support domain events.
/// </summary>
public static class SupportDomainEventsEntityExtensions
{
    /// <summary>
    /// Updates a property even if its setter is protected or private and adds <see cref="ISupportDomainEventsEntity.FieldUpdatedDomainEvent{TValue}" />.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the property value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyInfo">The property information.</param>
    /// <param name="newValue">The new value for the property.</param>
    /// <returns>The updated entity instance.</returns>
    public static TEntity SetPropertyIncludeValueUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        PropertyInfo propertyInfo,
        TValue newValue)
        where TEntity : IEntity
    {
        if (entity is ISupportDomainEventsEntity supportDomainEventsEntity)
        {
            var originalValue = propertyInfo.GetValue(entity);

            if (originalValue.IsValuesDifferent(newValue))
                supportDomainEventsEntity.AddFieldUpdatedEvent(propertyInfo, originalValue, newValue);
        }

        propertyInfo!.SetValue(entity, newValue);
        return entity;
    }

    /// <summary>
    /// Updates a property even if its setter is protected or private and adds <see cref="ISupportDomainEventsEntity.FieldUpdatedDomainEvent{TValue}" />.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the property value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="property">The property expression.</param>
    /// <param name="newValue">The new value for the property.</param>
    /// <returns>The updated entity instance.</returns>
    public static TEntity SetPropertyIncludeValueUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        Expression<Func<TEntity, TValue>> property,
        TValue newValue)
        where TEntity : IEntity
    {
        if (entity is ISupportDomainEventsEntity supportDomainEventsEntity)
        {
            var originalValue = property.Compile()(entity);

            if (originalValue.IsValuesDifferent(newValue))
                supportDomainEventsEntity.AddFieldUpdatedEvent(property.GetPropertyName(), originalValue, newValue);
        }

        entity.SetProperty(property, newValue);
        return entity;
    }

    /// <summary>
    /// Adds a field updated domain event to the entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyValueUpdatedDomainEvent">The field updated domain event.</param>
    /// <returns>The entity instance with the added domain event.</returns>
    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue> propertyValueUpdatedDomainEvent)
        where TEntity : ISupportDomainEventsEntity
    {
        entity.AddDomainEvent(
            propertyValueUpdatedDomainEvent,
            ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>());
        return entity;
    }

    /// <summary>
    /// Adds a field updated domain event to the entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="originalValue">The original value of the property.</param>
    /// <param name="newValue">The new value of the property.</param>
    /// <returns>The entity instance with the added domain event.</returns>
    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        string propertyName,
        TValue originalValue,
        TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddFieldUpdatedEvent(ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>.Create(propertyName, originalValue, newValue));
    }

    /// <summary>
    /// Adds a field updated domain event to the entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyInfo">The property information.</param>
    /// <param name="originalValue">The original value of the property.</param>
    /// <param name="newValue">The new value of the property.</param>
    /// <returns>The entity instance with the added domain event.</returns>
    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        PropertyInfo propertyInfo,
        TValue originalValue,
        TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddFieldUpdatedEvent(ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>.Create(propertyInfo.Name, originalValue, newValue));
    }

    /// <summary>
    /// Adds a field updated domain event to the entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="property">The property expression.</param>
    /// <param name="originalValue">The original value of the property.</param>
    /// <param name="newValue">The new value of the property.</param>
    /// <returns>The entity instance with the added domain event.</returns>
    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        Expression<Func<TEntity, TValue>> property,
        TValue originalValue,
        TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddFieldUpdatedEvent(ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>.Create(property.GetPropertyName(), originalValue, newValue));
    }

    /// <summary>
    /// Finds domain events of a specific type associated with the entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>List of domain events of the specified type.</returns>
    public static List<TEvent> FindDomainEvents<TEntity, TEvent>(this TEntity entity)
        where TEvent : ISupportDomainEventsEntity.DomainEvent
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is TEvent)
            .Select(p => p.Value.As<TEvent>())
            .ToList();
    }

    /// <summary>
    /// Finds field updated domain events for a specific field.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <typeparam name="TValue">Type of the field value.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>List of field updated domain events for the specified field.</returns>
    public static List<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>> FindFieldUpdatedDomainEvents<TEntity, TValue>(
        this TEntity entity,
        string propertyName)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>)
            .Select(p => p.Value.As<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>>())
            .Where(p => p.FieldName == propertyName)
            .ToList();
    }

    /// <summary>
    /// Gets all field updated domain events associated with the entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>List of all field updated domain events.</returns>
    public static List<ISupportDomainEventsEntity.FieldUpdatedDomainEvent> GetFieldUpdatedDomainEvents<TEntity>(this TEntity entity)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is ISupportDomainEventsEntity.FieldUpdatedDomainEvent)
            .Select(p => p.Value.As<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>())
            .ToList();
    }
}
