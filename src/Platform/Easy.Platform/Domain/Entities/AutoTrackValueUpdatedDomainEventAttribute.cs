using System.Reflection;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.Entities;

/// <summary>
/// Use this property on Entity and on Property you want to check to auto add <see cref="ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent" /> on update property one the entity.
/// Property with JsonIgnoreAttribute or IgnoreAddPropertyValueUpdatedDomainEventAttribute will be ignored
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class TrackValueUpdatedDomainEventAttribute : Attribute
{
}

public static class AutoAddPropertyValueUpdatedDomainEventEntityExtensions
{
    public static TEntity AutoAddPropertyValueUpdatedDomainEvent<TEntity>(this TEntity entity, TEntity existingOriginalEntity) where TEntity : class, IEntity, new()
    {
        if (entity.HasTrackValueUpdatedDomainEventAttribute())
            typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(IRowVersionEntity.ConcurrencyUpdateToken) && p.Name != nameof(IDateAuditedEntity.LastUpdatedDate))
                .Where(
                    propertyInfo => propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                                    propertyInfo.GetCustomAttribute<TrackValueUpdatedDomainEventAttribute>() != null)
                .Where(propertyInfo => propertyInfo.GetValue(entity).IsValuesDifferent(propertyInfo.GetValue(existingOriginalEntity)))
                .ForEach(
                    propertyInfo =>
                    {
                        entity.As<ISupportDomainEventsEntity<TEntity>>()
                            .AddPropertyValueUpdatedDomainEvent(
                                propertyInfo,
                                originalValue: propertyInfo.GetValue(existingOriginalEntity),
                                newValue: propertyInfo.GetValue(entity));
                    });

        return entity;
    }

    public static bool HasTrackValueUpdatedDomainEventAttribute<TEntity>(this TEntity entity) where TEntity : class, IEntity, new()
    {
        return entity is ISupportDomainEventsEntity<TEntity> &&
               typeof(TEntity).GetCustomAttribute(typeof(TrackValueUpdatedDomainEventAttribute), true) != null;
    }
}
