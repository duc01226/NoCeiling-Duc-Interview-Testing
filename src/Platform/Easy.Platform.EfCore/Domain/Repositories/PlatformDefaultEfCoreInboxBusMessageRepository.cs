using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public class PlatformDefaultEfCoreInboxBusMessageRepository<TDbContext>
    : PlatformEfCoreRootRepository<PlatformInboxBusMessage, string, TDbContext>, IPlatformInboxBusMessageRepository
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformDefaultEfCoreInboxBusMessageRepository(
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        DbContextOptions<TDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        dbContextOptions,
        serviceProvider)
    {
    }

    protected override bool IsDistributedTracingEnabled => false;

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IUnitOfWork uow)
    {
        return false;
    }
}
