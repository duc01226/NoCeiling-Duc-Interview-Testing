using Easy.Platform.Application.Persistence;
using Easy.Platform.Domain.Repositories;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public interface IPlatformInboxBusMessageRepository : IPlatformQueryableRootRepository<PlatformInboxBusMessage, string>
{
}

public interface IPlatformInboxBusMessageRepository<TDbContext> : IPlatformInboxBusMessageRepository where TDbContext : IPlatformDbContext<TDbContext>
{
}
