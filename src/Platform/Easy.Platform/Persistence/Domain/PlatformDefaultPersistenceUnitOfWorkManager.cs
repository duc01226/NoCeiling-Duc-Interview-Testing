using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Persistence.Domain;

public class PlatformDefaultPersistenceUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public PlatformDefaultPersistenceUnitOfWorkManager(
        Lazy<IPlatformCqrs> cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider) : base(cqrs, rootServiceProvider, serviceProvider)
    {
    }
}
