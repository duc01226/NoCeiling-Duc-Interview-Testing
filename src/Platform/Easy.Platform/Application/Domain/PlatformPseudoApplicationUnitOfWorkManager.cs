using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public override IUnitOfWork CreateNewUow()
    {
        return new PlatformPseudoApplicationUnitOfWork();
    }
}

internal sealed class PlatformPseudoApplicationUnitOfWork : PlatformUnitOfWork
{
    public override bool IsNoTransactionUow()
    {
        return true;
    }
}
