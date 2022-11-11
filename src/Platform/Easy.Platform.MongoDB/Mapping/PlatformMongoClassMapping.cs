using Easy.Platform.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Easy.Platform.MongoDB.Mapping;

public interface IPlatformMongoClassMapping
{
    void RegisterClassMap();
}

public abstract class PlatformMongoClassMapping : IPlatformMongoClassMapping
{
    public virtual bool AutoApplyGuidAsStringMappingConvention => false;

    public virtual bool AutoApplyEnumAsStringMappingConvention => false;

    public abstract void RegisterClassMap();

    public static void ApplyEnumAsStringMappingConvention<TEntity>(BsonClassMap<TEntity> cm)
    {
        foreach (var bsonMemberMap in cm.DeclaredMemberMaps)
            if (bsonMemberMap.MemberType.IsEnum)
            {
                bsonMemberMap.SetSerializer(
                    (IBsonSerializer)Activator.CreateInstance(
                        type: typeof(EnumSerializer<>).MakeGenericType(bsonMemberMap.MemberType),
                        args: new object[]
                        {
                            BsonType.String
                        }));
            }
            else
            {
                var underlyingNullableType = Nullable.GetUnderlyingType(bsonMemberMap.MemberType);

                // if It's nullable enum
                if (underlyingNullableType != null && underlyingNullableType.IsEnum)
                {
                    var enumType = underlyingNullableType;

                    bsonMemberMap.SetSerializer(
                        Activator
                            .CreateInstance(
                                type: typeof(NullableSerializer<>).MakeGenericType(enumType),
                                args: Activator.CreateInstance(
                                    type: typeof(EnumSerializer<>).MakeGenericType(enumType),
                                    args: BsonType.String))
                            .As<IBsonSerializer>());
                }
            }
    }

    public static void ApplyGuidAsStringMappingConvention<TEntity>(BsonClassMap<TEntity> cm)
    {
        foreach (var bsonMemberMap in cm.DeclaredMemberMaps)
            bsonMemberMap.MemberType
                .WhenValue(
                    typeof(Guid),
                    _ => bsonMemberMap.SetSerializer(
                        (IBsonSerializer)Activator.CreateInstance(
                            type: typeof(GuidSerializer),
                            args: new object[]
                            {
                                BsonType.String
                            })))
                .WhenValue(
                    typeof(Guid?),
                    _ => bsonMemberMap.SetSerializer(
                        (IBsonSerializer)Activator.CreateInstance(
                            type: typeof(NullableSerializer<Guid>),
                            args: new object[]
                            {
                                new GuidSerializer(BsonType.String)
                            })))
                .Execute();
    }

    public static void RegisterClassMapIfNotRegistered<TEntity>(Action<BsonClassMap<TEntity>> classMapInitializer)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TEntity))) BsonClassMap.RegisterClassMap(classMapInitializer);
    }

    public static void DefaultClassMapInitializer<T>(
        BsonClassMap<T> cm,
        bool autoApplyGuidAsStringMappingConvention = false,
        bool autoApplyEnumAsStringMappingConvention = false)
    {
        cm.AutoMap();
        cm.SetDiscriminatorIsRequired(true);
        cm.SetIgnoreExtraElements(true);
        if (autoApplyGuidAsStringMappingConvention)
            ApplyGuidAsStringMappingConvention(cm);
        if (autoApplyEnumAsStringMappingConvention)
            ApplyEnumAsStringMappingConvention(cm);
    }

    public static void DefaultEntityClassMapInitializer<TEntity, TPrimaryKey>(
        BsonClassMap<TEntity> cm,
        bool autoApplyGuidAsStringMappingConvention = false,
        bool autoApplyEnumAsStringMappingConvention = false) where TEntity : IEntity<TPrimaryKey>
    {
        DefaultClassMapInitializer(cm, autoApplyGuidAsStringMappingConvention, autoApplyEnumAsStringMappingConvention);
        cm.MapIdProperty(p => p.Id);
    }
}

/// <summary>
/// Used to map any entity which is inherited from <see cref="IEntity{TPrimaryKey}" />
/// </summary>
public abstract class PlatformMongoClassMapping<TEntity, TPrimaryKey> : PlatformMongoClassMapping
    where TEntity : IEntity<TPrimaryKey>
{
    public override void RegisterClassMap()
    {
        RegisterClassMapIfNotRegistered<TEntity>(ClassMapInitializer);
    }

    public virtual void ClassMapInitializer(BsonClassMap<TEntity> cm)
    {
        DefaultEntityClassMapInitializer<TEntity, TPrimaryKey>(cm, AutoApplyGuidAsStringMappingConvention, AutoApplyEnumAsStringMappingConvention);
    }
}
