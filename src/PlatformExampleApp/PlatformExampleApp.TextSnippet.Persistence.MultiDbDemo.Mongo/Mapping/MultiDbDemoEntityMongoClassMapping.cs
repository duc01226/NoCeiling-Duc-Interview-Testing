using Easy.Platform.MongoDB.Mapping;
using MongoDB.Bson.Serialization;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.Mapping;

public class MultiDbDemoEntityMongoClassMapping : PlatformMongoBaseEntityClassMapping<MultiDbDemoEntity, Guid>
{
    public override void ClassMapInitializer(BsonClassMap<MultiDbDemoEntity> cm)
    {
        base.ClassMapInitializer(cm);
    }
}
