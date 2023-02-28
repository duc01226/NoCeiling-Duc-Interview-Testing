using Easy.Platform.Domain.Entities;
using MongoDB.Bson.Serialization;

namespace Easy.Platform.MongoDB.Mapping;

/// <summary>
/// Used to map any entity which is inherited from <see cref="RootAuditedEntity{TEntity,TPrimaryKey,TUserId}"/>
/// </summary>
public abstract class PlatformMongoBaseAuditedEntityClassMapping<TEntity, TPrimaryKey, TUserId> : PlatformMongoBaseEntityClassMapping<TEntity, TPrimaryKey>
    where TEntity : RootAuditedEntity<TEntity, TPrimaryKey, TUserId>, new()
{
    public override void RegisterClassMap()
    {
        RegisterClassMapIfNotRegistered<Entity<TEntity, TPrimaryKey>>(BaseEntityClassMapInitializer);
        RegisterClassMapIfNotRegistered<RootAuditedEntity<TEntity, TPrimaryKey, TUserId>>(BaseAuditedEntityClassMapInitializer);
        RegisterClassMapIfNotRegistered<TEntity>(ClassMapInitializer);
    }

    public virtual void BaseAuditedEntityClassMapInitializer(
        BsonClassMap<RootAuditedEntity<TEntity, TPrimaryKey, TUserId>> cm)
    {
        DefaultClassMapInitializer(
            cm,
            AutoApplyGuidAsStringMappingConvention,
            AutoApplyEnumAsStringMappingConvention,
            AutoApplyTimeOnlyAsStringMappingConvention);
    }
}
