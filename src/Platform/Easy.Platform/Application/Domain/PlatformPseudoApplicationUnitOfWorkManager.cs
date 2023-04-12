using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public override IUnitOfWork CreateNewUow()
    {
        return new PlatformPseudoApplicationUnitOfWork().With(_ => _.CreatedByUnitOfWorkManager = this);
    }
}

internal sealed class PlatformPseudoApplicationUnitOfWork : PlatformUnitOfWork
{
    public override bool IsPseudoTransactionUow()
    {
        return true;
    }
}
