using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Extensions;
using Easy.Platform.Persistence.Domain;
using MongoDB.Driver;
using MongoDB.Driver.Core.Misc;
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

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetTable(uow).AsQueryable();
    }

    public override async Task<List<TSource>> ToListAsync<TSource>(
        IEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => DoToListAsync(source, cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.As<IMongoQueryable<TSource>>()
                    ?.Pipe(queryable => queryable != null ? queryable.ToQueryString : (Func<string>)null));

        return await DoToListAsync(source, cancellationToken);

        async Task<List<TSource>> DoToListAsync<TSource>(
            IEnumerable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            if (source.As<IMongoQueryable<TSource>>() == null) return source.ToList();

            // Use ToAsyncEnumerable behind the scene to support true enumerable like ef-core. Then can select anything and it will work.
            // Default as Enumerable from IQueryable still like Queryable which cause error query could not be translated for free select using constructor map for example
            return await ToAsyncEnumerable(source, cancellationToken).ToListAsync(cancellationToken).AsTask();
        }
    }

    public override async IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(
        IEnumerable<TSource> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using (var cursor = await source.As<IMongoQueryable<TSource>>().ToCursorAsync(cancellationToken).ConfigureAwait(false))
        {
            Ensure.IsNotNull(cursor, nameof(source));
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var item in cursor.Current) yield return item;

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    public override Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.As<IMongoQueryable<TSource>>().FirstOrDefaultAsync(cancellationToken);
    }

    public override async Task<TSource> FirstOrDefaultAsync<TSource>(
        IEnumerable<TSource> query,
        CancellationToken cancellationToken = default)
    {
        if (query.As<IMongoQueryable<TSource>>() != null)
            return await FirstOrDefaultAsync(query.As<IMongoQueryable<TSource>>(), cancellationToken);

        return query.FirstOrDefault();
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

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IUnitOfWork uow)
    {
        return false;
    }

    protected override void HandleDisposeUsingOnceTimeContextLogic<TResult>(
        IUnitOfWork uow,
        bool doesNeedKeepUowForQueryOrEnumerableExecutionLater,
        Expression<Func<TEntity, object>>[] loadRelatedEntities,
        TResult result)
    {
        uow.Dispose();
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
