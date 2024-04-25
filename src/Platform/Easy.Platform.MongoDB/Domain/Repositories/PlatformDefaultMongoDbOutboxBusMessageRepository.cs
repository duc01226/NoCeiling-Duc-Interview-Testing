using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public class PlatformDefaultMongoDbOutboxBusMessageRepository<TDbContext>
    : PlatformMongoDbRootRepository<PlatformOutboxBusMessage, string, TDbContext>, IPlatformOutboxBusMessageRepository
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformDefaultMongoDbOutboxBusMessageRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
    }

    protected override bool IsDistributedTracingEnabled => false;

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IPlatformUnitOfWork uow)
    {
        return false;
    }
}
