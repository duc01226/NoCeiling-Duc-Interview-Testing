using Easy.Platform.Domain.Entities;
using MongoDB.Bson.Serialization;

namespace Easy.Platform.MongoDB.Mapping;

/// <summary>
/// Used to map any entity which is inherited from <see cref="Entity{TEntity,TPrimaryKey}"/>
/// </summary>
public abstract class PlatformMongoBaseEntityClassMapping<TEntity, TPrimaryKey> : PlatformMongoClassMapping<TEntity, TPrimaryKey>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
    public override bool AutoApplyGuidAsStringMappingConvention => true;

    public override bool AutoApplyEnumAsStringMappingConvention => true;

    public override void RegisterClassMap()
    {
        RegisterClassMapIfNotRegistered<Entity<TEntity, TPrimaryKey>>(BaseEntityClassMapInitializer);
        RegisterClassMapIfNotRegistered<TEntity>(ClassMapInitializer);
    }

    public override void ClassMapInitializer(BsonClassMap<TEntity> cm)
    {
        DefaultClassMapInitializer(cm, AutoApplyGuidAsStringMappingConvention, AutoApplyEnumAsStringMappingConvention);
    }

    public virtual void BaseEntityClassMapInitializer(BsonClassMap<Entity<TEntity, TPrimaryKey>> cm)
    {
        DefaultEntityClassMapInitializer<Entity<TEntity, TPrimaryKey>, TPrimaryKey>(
            cm,
            AutoApplyGuidAsStringMappingConvention,
            AutoApplyEnumAsStringMappingConvention);
    }
}
