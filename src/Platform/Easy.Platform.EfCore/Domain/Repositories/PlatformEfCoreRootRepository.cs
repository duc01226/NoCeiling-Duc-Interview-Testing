using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.EfCore.Domain.Repositories;

public abstract class PlatformEfCoreRootRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(unitOfWorkManager, cqrs, serviceProvider)
    {
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow)
    {
        return GetTable(uow).AsQueryable();
    }
}

public abstract class PlatformDefaultEfCoreRootRepository<TEntity, TPrimaryKey, TDbContext> : PlatformEfCoreRootRepository<TEntity, TPrimaryKey, TDbContext>
    where TEntity : RootEntity<TEntity, TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    protected PlatformDefaultEfCoreRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}
