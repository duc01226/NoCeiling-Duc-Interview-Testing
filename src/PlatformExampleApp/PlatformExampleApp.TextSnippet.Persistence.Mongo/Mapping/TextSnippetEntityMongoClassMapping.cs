using Easy.Platform.MongoDB.Mapping;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo.Mapping;

public class TextSnippetEntityMongoClassMapping : PlatformMongoBaseAuditedEntityClassMapping<TextSnippetEntity, string, string?>
{
}
