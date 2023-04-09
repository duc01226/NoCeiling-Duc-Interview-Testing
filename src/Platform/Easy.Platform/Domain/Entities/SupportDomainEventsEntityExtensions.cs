using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.Entities;

public static class SupportDomainEventsEntityExtensions
{
    /// <summary>
    /// Update property even if it's setter protected or private and add <see cref="ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent{TValue}" />
    /// </summary>
    public static TEntity SetPropertyIncludeValueUpdatedEvent<TEntity, TValue>(this TEntity entity, PropertyInfo propertyInfo, TValue newValue)
        where TEntity : IEntity
    {
        if (entity is ISupportDomainEventsEntity supportDomainEventsEntity)
        {
            var originalValue = propertyInfo.GetValue(entity);

            if (originalValue.IsValuesDifferent(newValue))
                supportDomainEventsEntity.AddPropertyValueUpdatedDomainEvent(propertyInfo, originalValue, newValue);
        }

        propertyInfo!.SetValue(entity, newValue);
        return entity;
    }

    /// <summary>
    /// Update property even if it's setter protected or private and add <see cref="ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent{TValue}" />
    /// </summary>
    public static TEntity SetPropertyIncludeValueUpdatedEvent<TEntity, TValue>(this TEntity entity, Expression<Func<TEntity, TValue>> property, TValue newValue)
        where TEntity : IEntity
    {
        if (entity is ISupportDomainEventsEntity supportDomainEventsEntity)
        {
            var originalValue = property.Compile()(entity);

            if (originalValue.IsValuesDifferent(newValue))
                supportDomainEventsEntity.AddPropertyValueUpdatedDomainEvent(property.GetPropertyName(), originalValue, newValue);
        }

        entity.SetProperty(property, newValue);
        return entity;
    }

    public static TEntity AddPropertyValueUpdatedDomainEvent<TEntity, TValue>(
        this TEntity entity,
        ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue> propertyValueUpdatedDomainEvent)
        where TEntity : ISupportDomainEventsEntity
    {
        entity.AddDomainEvent(
            propertyValueUpdatedDomainEvent,
            ISupportDomainEventsEntity.DomainEvent.GetDefaultDomainEventName<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent>());
        return entity;
    }

    public static TEntity AddPropertyValueUpdatedDomainEvent<TEntity, TValue>(this TEntity entity, string propertyName, TValue originalValue, TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddPropertyValueUpdatedDomainEvent(
            ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>.Create(propertyName, originalValue, newValue));
    }

    public static TEntity AddPropertyValueUpdatedDomainEvent<TEntity, TValue>(this TEntity entity, PropertyInfo propertyInfo, TValue originalValue, TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddPropertyValueUpdatedDomainEvent(
            ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>.Create(propertyInfo.Name, originalValue, newValue));
    }

    public static TEntity AddPropertyValueUpdatedDomainEvent<TEntity, TValue>(
        this TEntity entity,
        Expression<Func<TEntity, TValue>> property,
        TValue originalValue,
        TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddPropertyValueUpdatedDomainEvent(
            ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>.Create(property.GetPropertyName(), originalValue, newValue));
    }

    public static List<TEvent> FindDomainEvents<TEntity, TEvent>(this TEntity entity)
        where TEvent : ISupportDomainEventsEntity.DomainEvent
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is TEvent)
            .Select(p => p.Value.As<TEvent>())
            .ToList();
    }

    public static List<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>> FindPropertyValueUpdatedDomainEvents<TEntity, TValue>(
        this TEntity entity,
        string propertyName)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>)
            .Select(p => p.Value.As<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent<TValue>>())
            .Where(p => p.PropertyName == propertyName)
            .ToList();
    }

    public static List<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent> GetPropertyValueUpdatedDomainEvents<TEntity>(this TEntity entity)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent)
            .Select(p => p.Value.As<ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent>())
            .ToList();
    }
}
