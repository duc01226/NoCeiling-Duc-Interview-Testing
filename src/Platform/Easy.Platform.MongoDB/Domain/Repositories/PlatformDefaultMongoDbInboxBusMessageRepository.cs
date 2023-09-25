using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public class PlatformDefaultMongoDbInboxBusMessageRepository<TDbContext>
    : PlatformMongoDbRootRepository<PlatformInboxBusMessage, string, TDbContext>, IPlatformInboxBusMessageRepository
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformDefaultMongoDbInboxBusMessageRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }

    protected override bool IsDistributedTracingEnabled => false;

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IUnitOfWork uow)
    {
        return false;
    }
}
