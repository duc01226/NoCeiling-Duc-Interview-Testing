using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.UnitOfWork;
using Easy.Platform.Persistence.Domain;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public abstract class PlatformMongoDbRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformPersistenceRepository<TEntity, TPrimaryKey, IPlatformMongoDbPersistenceUnitOfWork<TDbContext>, TDbContext>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }

    public virtual IMongoCollection<TEntity> Table => DbContext.GetCollection<TEntity>();

    public virtual IMongoCollection<TEntity> GetTable(IUnitOfWork uow)
    {
        return GetUowDbContext(uow).GetCollection<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return GetTable(uow).AsQueryable();
    }

    public override Task<List<TSource>> ToListAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.As<IMongoQueryable<TSource>>().ToListAsync(cancellationToken);
    }

    public override Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.As<IMongoQueryable<TSource>>().FirstOrDefaultAsync(cancellationToken);
    }

    public override Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.As<IMongoQueryable<TSource>>().FirstAsync(cancellationToken);
    }

    public override Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.As<IMongoQueryable<TSource>>().CountAsync(cancellationToken);
    }

    public override Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.As<IMongoQueryable<TSource>>().AnyAsync(cancellationToken);
    }
}

public abstract class PlatformMongoDbRootRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformMongoDbRepository<TEntity, TPrimaryKey, TDbContext>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}
