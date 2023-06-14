using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.Entities;

public static class SupportDomainEventsEntityExtensions
{
    /// <summary>
    /// Update property even if it's setter protected or private and add <see cref="ISupportDomainEventsEntity.FieldUpdatedDomainEvent{TValue}" />
    /// </summary>
    public static TEntity SetPropertyIncludeValueUpdatedEvent<TEntity, TValue>(this TEntity entity, PropertyInfo propertyInfo, TValue newValue)
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
    /// Update property even if it's setter protected or private and add <see cref="ISupportDomainEventsEntity.FieldUpdatedDomainEvent{TValue}" />
    /// </summary>
    public static TEntity SetPropertyIncludeValueUpdatedEvent<TEntity, TValue>(this TEntity entity, Expression<Func<TEntity, TValue>> property, TValue newValue)
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

    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(this TEntity entity, string propertyName, TValue originalValue, TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddFieldUpdatedEvent(
            ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>.Create(propertyName, originalValue, newValue));
    }

    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(this TEntity entity, PropertyInfo propertyInfo, TValue originalValue, TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddFieldUpdatedEvent(
            ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>.Create(propertyInfo.Name, originalValue, newValue));
    }

    public static TEntity AddFieldUpdatedEvent<TEntity, TValue>(
        this TEntity entity,
        Expression<Func<TEntity, TValue>> property,
        TValue originalValue,
        TValue newValue)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.AddFieldUpdatedEvent(
            ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>.Create(property.GetPropertyName(), originalValue, newValue));
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

    public static List<ISupportDomainEventsEntity.FieldUpdatedDomainEvent> GetFieldUpdatedDomainEvents<TEntity>(this TEntity entity)
        where TEntity : ISupportDomainEventsEntity
    {
        return entity.GetDomainEvents()
            .Where(p => p.Value is ISupportDomainEventsEntity.FieldUpdatedDomainEvent)
            .Select(p => p.Value.As<ISupportDomainEventsEntity.FieldUpdatedDomainEvent>())
            .ToList();
    }
}
