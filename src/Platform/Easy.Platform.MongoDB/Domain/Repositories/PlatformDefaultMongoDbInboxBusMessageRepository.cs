using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public class PlatformDefaultMongoDbInboxBusMessageRepository<TDbContext>
    : PlatformDefaultMongoDbRootRepository<PlatformInboxBusMessage, string, TDbContext>, IPlatformInboxBusMessageRepository
    where TDbContext : IPlatformMongoDbContext<TDbContext>
{
    public PlatformDefaultMongoDbInboxBusMessageRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(unitOfWorkManager, cqrs, serviceProvider)
    {
    }
}
