using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public class PlatformDefaultEfCoreInboxBusMessageRepository<TDbContext>
    : PlatformEfCoreRootRepository<PlatformInboxBusMessage, string, TDbContext>, IPlatformInboxBusMessageRepository
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformDefaultEfCoreInboxBusMessageRepository(
        DbContextOptions<TDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        dbContextOptions,
        serviceProvider)
    {
    }

    protected override bool IsDistributedTracingEnabled => false;

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IPlatformUnitOfWork uow)
    {
        return false;
    }
}
