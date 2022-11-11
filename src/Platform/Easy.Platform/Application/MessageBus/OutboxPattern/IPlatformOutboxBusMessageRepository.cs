using Easy.Platform.Domain.Repositories;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public interface IPlatformOutboxBusMessageRepository : IPlatformQueryableRootRepository<PlatformOutboxBusMessage, string>
{
}
