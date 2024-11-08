using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public PlatformPseudoApplicationUnitOfWorkManager(
        Lazy<IPlatformCqrs> cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider) : base(cqrs, rootServiceProvider, serviceProvider)
    {
    }
}
