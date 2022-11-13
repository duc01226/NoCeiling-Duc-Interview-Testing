using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public class PlatformDefaultMongoDbOutboxBusMessageRepository<TDbContext>
    : PlatformMongoDbRootRepository<PlatformOutboxBusMessage, string, TDbContext>, IPlatformOutboxBusMessageRepository
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformDefaultMongoDbOutboxBusMessageRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}
