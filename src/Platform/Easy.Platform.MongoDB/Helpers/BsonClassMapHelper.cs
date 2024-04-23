using Easy.Platform.MongoDB.Mapping;
using MongoDB.Bson.Serialization;

namespace Easy.Platform.MongoDB.Helpers;

internal static class BsonClassMapHelper
{
    public static void DefaultClassMapInitializer<TClassMap>(BsonClassMap<TClassMap> cm)
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true);
        PlatformMongoClassMapping.ApplyEnumAsStringMappingConvention(cm);
        PlatformMongoClassMapping.ApplyGuidAsStringMappingConvention(cm);
        PlatformMongoClassMapping.ApplyTimeOnlyAsStringMappingConvention(cm);
    }

    public static void TryRegisterClassMapWithDefaultInitializer<TClassMap>()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TClassMap)))
            BsonClassMap.RegisterClassMap<TClassMap>(DefaultClassMapInitializer);
    }
}
