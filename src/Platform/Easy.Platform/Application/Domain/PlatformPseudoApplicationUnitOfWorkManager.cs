using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public PlatformPseudoApplicationUnitOfWorkManager(
        Lazy<IPlatformCqrs> cqrs,
        IPlatformRootServiceProvider rootServiceProvider) : base(cqrs, rootServiceProvider)
    {
    }

    public override IPlatformUnitOfWork CreateNewUow(bool isUsingOnceTransientUow)
    {
        return new PlatformPseudoApplicationUnitOfWork(RootServiceProvider)
            .With(uow => uow.CreatedByUnitOfWorkManager = this)
            .With(uow => uow.IsUsingOnceTransientUow = isUsingOnceTransientUow);
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

    protected override Task InternalSaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
