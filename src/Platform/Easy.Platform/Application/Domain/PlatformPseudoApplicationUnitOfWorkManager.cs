using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public PlatformPseudoApplicationUnitOfWorkManager(
        IPlatformCqrs currentSameScopeCqrs,
        IPlatformRootServiceProvider rootServiceProvider) : base(currentSameScopeCqrs, rootServiceProvider)
    {
    }

    public override IUnitOfWork CreateNewUow()
    {
        return new PlatformPseudoApplicationUnitOfWork(RootServiceProvider)
            .With(_ => _.CreatedByUnitOfWorkManager = this);
    }
}

internal sealed class PlatformPseudoApplicationUnitOfWork : PlatformUnitOfWork
{
    public PlatformPseudoApplicationUnitOfWork(IPlatformRootServiceProvider rootServiceProvider) : base(rootServiceProvider)
    {
    }

    public override bool IsPseudoTransactionUow()
    {
        return true;
    }

    public override bool MustKeepUowForQuery()
    {
        return false;
    }

    public override bool DoesSupportParallelQuery()
    {
        return true;
    }
}
