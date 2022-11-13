using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.EfCore.Domain.Repositories;

public class PlatformDefaultEfCoreInboxBusMessageRepository<TDbContext>
    : PlatformEfCoreRootRepository<PlatformInboxBusMessage, string, TDbContext>, IPlatformInboxBusMessageRepository
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformDefaultEfCoreInboxBusMessageRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}
