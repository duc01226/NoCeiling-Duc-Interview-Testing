using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.UnitOfWork;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public abstract class PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformPersistenceRepository<TEntity, TPrimaryKey, IPlatformEfCorePersistenceUnitOfWork<TDbContext>, TDbContext>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }

    public virtual DbSet<TEntity> Table => DbContext.Set<TEntity>();

    public virtual DbSet<TEntity> GetTable(IUnitOfWork uow)
    {
        return GetUowDbContext(uow).Set<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return GetTable(uow).Pipe(p => loadRelatedEntities.ForEach(loadWithEntityPropPath => p.Include(loadWithEntityPropPath))).AsNoTracking().AsQueryable();
    }

    public override Task<List<TSource>> ToListAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.ToListAsync(cancellationToken);
    }

    public override Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.FirstOrDefaultAsync(cancellationToken);
    }

    public override Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.FirstAsync(cancellationToken);
    }

    public override Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.CountAsync(cancellationToken);
    }

    public override Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.AnyAsync(cancellationToken);
    }
}

public abstract class PlatformEfCoreRootRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return GetTable(uow)
            .PipeIf(
                loadRelatedEntities.Any(),
                p => loadRelatedEntities.Aggregate(p.AsQueryable(), (query, loadRelatedEntityFn) => query.Include(loadRelatedEntityFn)))
            .AsQueryable();
    }
}
