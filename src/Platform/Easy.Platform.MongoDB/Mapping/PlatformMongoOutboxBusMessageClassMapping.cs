using Easy.Platform.Application.MessageBus.OutboxPattern;

namespace Easy.Platform.MongoDB.Mapping;

public abstract class PlatformMongoOutboxBusMessageClassMapping : PlatformMongoBaseEntityClassMapping<PlatformOutboxBusMessage, string>
{
}

public class PlatformDefaultMongoOutboxBusMessageClassMapping : PlatformMongoOutboxBusMessageClassMapping
{
}
