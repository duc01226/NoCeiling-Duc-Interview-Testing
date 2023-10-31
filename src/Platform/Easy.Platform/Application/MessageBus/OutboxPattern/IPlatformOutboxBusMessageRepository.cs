using Easy.Platform.Application.Persistence;
using Easy.Platform.Domain.Repositories;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public interface IPlatformOutboxBusMessageRepository : IPlatformQueryableRootRepository<PlatformOutboxBusMessage, string>
{
}

public interface IPlatformOutboxBusMessageRepository<TDbContext> : IPlatformOutboxBusMessageRepository where TDbContext : IPlatformDbContext<TDbContext>
{
}
