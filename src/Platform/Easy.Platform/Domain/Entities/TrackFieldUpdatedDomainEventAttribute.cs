using System.Reflection;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.Entities;

/// <summary>
/// Use this property on Entity and on Property you want to check to auto add <see cref="ISupportDomainEventsEntity.FieldUpdatedDomainEvent" /> on update property one the entity.
/// Property with JsonIgnoreAttribute or IgnoreAddFieldUpdatedEventAttribute will be ignored
/// </summary>
/// <remarks>
/// The TrackFieldUpdatedDomainEventAttribute is a custom attribute in C# that is used to automatically add a FieldUpdatedDomainEvent to an entity or property when it is updated. This attribute can be applied to both classes and properties.
/// <br />
/// When applied to a class, it indicates that any update to the properties of the class should trigger the FieldUpdatedDomainEvent. When applied to a property, it indicates that an update to that specific property should trigger the FieldUpdatedDomainEvent.
/// <br />
/// This attribute is useful for tracking changes to the fields of an entity and can be used to trigger certain actions when a field is updated.
/// <br />
/// Please note that properties with JsonIgnoreAttribute will be ignored, meaning that updates to these properties will not trigger the FieldUpdatedDomainEvent.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class TrackFieldUpdatedDomainEventAttribute : Attribute
{
}

public static class AutoAddFieldUpdatedEventEntityExtensions
{
    /// <summary>
    /// The AutoAddFieldUpdatedEvent method is an extension method for entities that implement the IEntity interface. The purpose of this method is to automatically track and add domain events for any changes made to the fields of an entity.
    /// <br />
    /// The method works by checking if the entity has the TrackFieldUpdatedDomainEventAttribute attribute. If it does, the method iterates over all public instance properties of the entity, excluding certain ones like ConcurrencyUpdateToken and LastUpdatedDate.
    /// <br />
    /// For each property, it checks if it has the JsonIgnoreAttribute and TrackFieldUpdatedDomainEventAttribute attributes. If the property has the TrackFieldUpdatedDomainEventAttribute and does not have the JsonIgnoreAttribute, it then checks if the current value of the property is different from the original value.
    /// <br />
    /// If the value has changed, it adds a field updated event to the entity using the AddFieldUpdatedEvent method. This event includes the property that changed, the original value, and the new value.
    /// <br />
    /// This method is useful in scenarios where you want to keep track of changes made to an entity's fields, for example, in an event sourcing architecture or for auditing purposes.
    /// </summary>
    public static TEntity AutoAddFieldUpdatedEvent<TEntity>(this TEntity entity, TEntity existingOriginalEntity) where TEntity : class, IEntity, new()
    {
        if (entity.HasTrackValueUpdatedDomainEventAttribute())
            typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(IRowVersionEntity.ConcurrencyUpdateToken) && p.Name != nameof(IDateAuditedEntity.LastUpdatedDate))
                .Where(
                    propertyInfo => propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                                    propertyInfo.GetCustomAttribute<TrackFieldUpdatedDomainEventAttribute>() != null)
                .Where(propertyInfo => propertyInfo.GetValue(entity).IsValuesDifferent(propertyInfo.GetValue(existingOriginalEntity)))
                .ForEach(
                    propertyInfo =>
                    {
                        entity.As<ISupportDomainEventsEntity<TEntity>>()
                            .AddFieldUpdatedEvent(
                                propertyInfo,
                                originalValue: propertyInfo.GetValue(existingOriginalEntity),
                                newValue: propertyInfo.GetValue(entity));
                    });

        return entity;
    }

    /// <summary>
    /// This method checks if the entity supports domain events and if it has the TrackFieldUpdatedDomainEventAttribute attribute.
    /// <br />
    /// In the context of the provided code, this method is used to determine whether an entity should automatically add a field updated event. Specifically, it's used in the AutoAddFieldUpdatedEvent method to decide if the entity's properties should be inspected for changes. If the entity has the TrackFieldUpdatedDomainEventAttribute, the method will iterate over the entity's properties, excluding certain ones, and check if their values have changed. If a property's value has changed, a field updated event is added.
    /// </summary>
    public static bool HasTrackValueUpdatedDomainEventAttribute<TEntity>(this TEntity entity) where TEntity : IEntity
    {
        return entity is ISupportDomainEventsEntity<TEntity> &&
               typeof(TEntity).GetCustomAttribute(typeof(TrackFieldUpdatedDomainEventAttribute), true) != null;
    }
}
