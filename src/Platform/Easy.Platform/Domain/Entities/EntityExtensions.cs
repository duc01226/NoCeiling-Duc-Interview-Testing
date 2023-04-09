#nullable enable
using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Exceptions;

namespace Easy.Platform.Domain.Entities;

public static class PlatformEntityExtensions
{
    /// <summary>
    /// Update all properties value from current entity to target entity even if it's setter protected or private and trigger <see cref="ISupportDomainEventsEntity.PropertyValueUpdatedDomainEvent" /> <br />
    /// ignorePropPredicate: (sourceEntity, propInfo) => bool
    /// </summary>
    public static TEntity SetAllPropertiesIncludeValueUpdatedEvent<TEntity, TPrimaryKey>(
        this TEntity entity,
        TEntity targetEntity,
        Expression<Func<TEntity, PropertyInfo, bool>>? ignorePropPredicate = null)
        where TEntity : class, IEntity<TPrimaryKey>
    {
        typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(IEntity<TPrimaryKey>.Id) && p.Name != nameof(IRowVersionEntity.ConcurrencyUpdateToken))
            .WhereIf(ignorePropPredicate != null, propertyInfo => !ignorePropPredicate!.Compile()(entity, propertyInfo))
            .ForEach(
                propertyInfo => targetEntity.SetPropertyIncludeValueUpdatedEvent(propertyInfo, newValue: propertyInfo!.GetValue(entity)));

        return targetEntity;
    }

    /// <summary>
    /// Update all properties value from current entity to target entity even if it's setter protected or private <br />
    /// ignorePropPredicate: (sourceEntity, propInfo) => bool
    /// </summary>
    public static TEntity SetAllProperties<TEntity, TPrimaryKey>(
        this TEntity entity,
        TEntity targetEntity,
        Expression<Func<TEntity, PropertyInfo, bool>>? ignorePropPredicate = null)
        where TEntity : class, IEntity<TPrimaryKey>
    {
        typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(IEntity<TPrimaryKey>.Id) && p.Name != nameof(IRowVersionEntity.ConcurrencyUpdateToken))
            .WhereIf(ignorePropPredicate != null, propertyInfo => !ignorePropPredicate!.Compile()(entity, propertyInfo))
            .ForEach(
                propertyInfo => propertyInfo.SetValue(targetEntity, propertyInfo!.GetValue(entity)));

        return targetEntity;
    }

    public static T? FindById<T, TId>(this IEnumerable<T> entities, TId id) where T : IEntity<TId>
    {
        return entities.FirstOrDefault(p => p.Id != null && p.Id.Equals(id));
    }

    public static T GetById<T, TId>(this IEnumerable<T> entities, TId id, Func<Exception>? notFoundException = null) where T : IEntity<TId>
    {
        return entities.FindById(id) ??
               (notFoundException != null ? throw notFoundException() : throw new PlatformDomainEntityNotFoundException<T>(id?.ToString()));
    }

    public static bool IsAuditedUserEntity<TEntity>(this TEntity entity) where TEntity : IEntity
    {
        return entity is IUserAuditedEntity && entity.GetType().FindMatchedGenericType(typeof(IFullAuditedEntity<>)) != null;
    }

    public static Type GetAuditedUserIdType<TEntity>(this TEntity entity) where TEntity : IEntity
    {
        return entity.GetType().FindMatchedGenericType(typeof(IFullAuditedEntity<>)).GenericTypeArguments[0];
    }
}
