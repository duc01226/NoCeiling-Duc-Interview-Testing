using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public class PlatformDefaultEfCoreOutboxBusMessageRepository<TDbContext>
    : PlatformEfCoreRootRepository<PlatformOutboxBusMessage, string, TDbContext>, IPlatformOutboxBusMessageRepository
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformDefaultEfCoreOutboxBusMessageRepository(
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

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IUnitOfWork uow)
    {
        return false;
    }
}
