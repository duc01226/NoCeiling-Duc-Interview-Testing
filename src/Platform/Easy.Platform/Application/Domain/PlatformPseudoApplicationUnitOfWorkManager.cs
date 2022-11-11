using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Application.Domain;

internal class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public override IUnitOfWork CreateNewUow()
    {
        return new PlatformPseudoApplicationUnitOfWork();
    }
}

internal class PlatformPseudoApplicationUnitOfWork : PlatformUnitOfWork
{
    public override bool IsNoTransactionUow()
    {
        return true;
    }
}
