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
        IServiceProvider serviceProvider) : base(cqrs)
    {
        ServiceProvider = serviceProvider;
    }

    public override IUnitOfWork CreateNewUow()
    {
        try
        {
            CreateNewUowLock.WaitAsync();

            // Doing create scope because IUnitOfWork resolve with DbContext, and DbContext lifetime is usually scoped to support resolve db context
            // to use it directly in application layer in some project or cases without using repository.
            // But we still want to support Uow create new like transient, each uow associated with new db context
            // So that we can begin/destroy uow separately

            var newScope = ServiceProvider.CreateScope();

            var uow = new PlatformAggregatedPersistenceUnitOfWork(
                    newScope.ServiceProvider.GetServices<IUnitOfWork>()
                        .Select(p => p.With(_ => _.CreatedByUnitOfWorkManager = this))
                        .ToList(),
                    associatedServiceScope: newScope)
                .With(_ => _.CreatedByUnitOfWorkManager = this);

            FreeCreatedUnitOfWorks.Add(uow);

            return uow;
        }
        finally
        {
            CreateNewUowLock.Release();
        }
    }
}
