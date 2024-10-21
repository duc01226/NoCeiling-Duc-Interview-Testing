using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public PlatformPseudoApplicationUnitOfWorkManager(
        IPlatformCqrs cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider) : base(cqrs, rootServiceProvider, serviceProvider)
    {
    }

    public override IPlatformUnitOfWork CreateNewUow(bool isUsingOnceTransientUow)
    {
        return new PlatformPseudoApplicationUnitOfWork(RootServiceProvider, RootServiceProvider.GetService<ILoggerFactory>())
            .With(uow => uow.CreatedByUnitOfWorkManager = this)
            .With(uow => uow.IsUsingOnceTransientUow = isUsingOnceTransientUow);
    }
}

internal sealed class PlatformPseudoApplicationUnitOfWork : PlatformUnitOfWork
{
    public PlatformPseudoApplicationUnitOfWork(
        IPlatformRootServiceProvider rootServiceProvider,
        ILoggerFactory loggerFactory) : base(rootServiceProvider, loggerFactory)
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
