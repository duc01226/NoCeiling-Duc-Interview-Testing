using Easy.Platform.Domain.Entities;
using Easy.Platform.MongoDB.Serializer;
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

    public virtual bool AutoApplyTimeOnlyAsStringMappingConvention => false;

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
        ApplySerializerMappingConvention<TEntity, GuidSerializer, Guid>(
            cm,
            new object[]
            {
                BsonType.String
            });
        ApplySerializerMappingConvention<TEntity, NullableSerializer<Guid>, Guid?>(
            cm,
            new object[]
            {
                new GuidSerializer(BsonType.String)
            });
    }

    public static void ApplyTimeOnlyAsStringMappingConvention<TEntity>(BsonClassMap<TEntity> cm)
    {
        ApplySerializerMappingConvention<TEntity, PlatformTimeOnlyToStringMongoDbSerializer, TimeOnly>(cm);
        ApplySerializerMappingConvention<TEntity, PlatformNullableTimeOnlyToStringMongoDbSerializer, TimeOnly?>(cm);
    }

    public static void ApplySerializerMappingConvention<TEntity, TSerializer, TValue>(
        BsonClassMap<TEntity> cm,
        object[] createSerializerConstructorArgs = null) where TSerializer : IBsonSerializer<TValue>
    {
        foreach (var bsonMemberMap in cm.DeclaredMemberMaps)
            bsonMemberMap.MemberType
                .WhenValue(
                    typeof(TValue),
                    _ => bsonMemberMap.SetSerializer(
                        (IBsonSerializer<TValue>)Activator.CreateInstance(type: typeof(TSerializer), args: createSerializerConstructorArgs)))
                .Execute();
    }

    public static void RegisterClassMapIfNotRegistered<TEntity>(Action<BsonClassMap<TEntity>> classMapInitializer)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TEntity))) BsonClassMap.RegisterClassMap(classMapInitializer);
    }

    public static void DefaultClassMapInitializer<T>(
        BsonClassMap<T> cm,
        bool autoApplyGuidAsStringMappingConvention,
        bool autoApplyEnumAsStringMappingConvention,
        bool autoApplyTimeOnlyAsStringMappingConvention)
    {
        cm.AutoMap();
        cm.SetDiscriminatorIsRequired(true);
        cm.SetIgnoreExtraElements(true);
        if (autoApplyGuidAsStringMappingConvention)
            ApplyGuidAsStringMappingConvention(cm);
        if (autoApplyEnumAsStringMappingConvention)
            ApplyEnumAsStringMappingConvention(cm);
        if (autoApplyTimeOnlyAsStringMappingConvention)
            ApplyTimeOnlyAsStringMappingConvention(cm);
    }

    public static void DefaultEntityClassMapInitializer<TEntity, TPrimaryKey>(
        BsonClassMap<TEntity> cm,
        bool autoApplyGuidAsStringMappingConvention,
        bool autoApplyEnumAsStringMappingConvention,
        bool autoApplyTimeOnlyAsStringMappingConvention) where TEntity : IEntity<TPrimaryKey>
    {
        DefaultClassMapInitializer(
            cm,
            autoApplyGuidAsStringMappingConvention,
            autoApplyEnumAsStringMappingConvention,
            autoApplyTimeOnlyAsStringMappingConvention);
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
        DefaultEntityClassMapInitializer<TEntity, TPrimaryKey>(
            cm,
            AutoApplyGuidAsStringMappingConvention,
            AutoApplyEnumAsStringMappingConvention,
            AutoApplyTimeOnlyAsStringMappingConvention);
    }
}
