using Easy.Platform.Application.MessageBus.InboxPattern;

namespace Easy.Platform.MongoDB.Mapping;

public abstract class PlatformMongoInboxBusMessageClassMapping : PlatformMongoBaseEntityClassMapping<PlatformInboxBusMessage, string>
{
}

public sealed class PlatformDefaultMongoInboxBusMessageClassMapping : PlatformMongoInboxBusMessageClassMapping
{
}
