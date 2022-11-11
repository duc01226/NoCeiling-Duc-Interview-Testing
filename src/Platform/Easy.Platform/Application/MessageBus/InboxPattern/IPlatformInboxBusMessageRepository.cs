using Easy.Platform.Domain.Repositories;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public interface IPlatformInboxBusMessageRepository : IPlatformQueryableRootRepository<PlatformInboxBusMessage, string>
{
}
