using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Persistence.Domain;

public class PlatformDefaultPersistenceUnitOfWorkManager : PlatformUnitOfWorkManager
{
    protected readonly IServiceProvider ServiceProvider;

    public PlatformDefaultPersistenceUnitOfWorkManager(
        IPlatformCqrs cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider) : base(cqrs, rootServiceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public override IUnitOfWork CreateNewUow(bool isUsingOnceTransientUow)
    {
        // Doing create scope because IUnitOfWork resolve with DbContext, and DbContext lifetime is usually scoped to support resolve db context
        // to use it directly in application layer in some project or cases without using repository.
        // But we still want to support Uow create new like transient, each uow associated with new db context
        // So that we can begin/destroy uow separately

        var newScope = ServiceProvider.CreateScope();

        var uow = new PlatformAggregatedPersistenceUnitOfWork(
                RootServiceProvider,
                newScope.ServiceProvider.GetServices<IUnitOfWork>()
                    .Select(
                        p => p
                            .With(_ => _.CreatedByUnitOfWorkManager = this)
                            .With(_ => _.IsUsingOnceTransientUow = isUsingOnceTransientUow))
                    .ToList(),
                associatedServiceScope: newScope)
            .With(uow => uow.CreatedByUnitOfWorkManager = this);

        uow.OnDisposedActions.Add(async () => await Task.Run(() => uow.CreatedByUnitOfWorkManager.RemoveAllInactiveUow()));

        FreeCreatedUnitOfWorks.TryAdd(uow.Id, uow);

        return uow;
    }
}
