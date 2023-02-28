using Easy.Platform.MongoDB.Mapping;
using MongoDB.Bson.Serialization;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo.Mapping;

public class TextSnippetEntityMongoClassMapping : PlatformMongoBaseAuditedEntityClassMapping<TextSnippetEntity, Guid, Guid?>
{
}
