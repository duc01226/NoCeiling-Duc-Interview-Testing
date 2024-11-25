using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Easy.Platform.Application.Persistence;
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
    public PlatformMongoDbRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
    }

    protected override bool DoesSupportParallelExecution()
    {
        return true;
    }

    protected override bool DoesSupportSingletonUow()
    {
        return true;
    }

    protected override bool IsPseudoTransactionUow()
    {
        return true;
    }

    public virtual IMongoCollection<TEntity> GetTable(IPlatformUnitOfWork uow)
    {
        return GetUowDbContext(uow).GetCollection<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IPlatformUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetTable(uow).AsQueryable();
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public override async Task<List<TSource>> ToListAsync<TSource>(
        IEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        LogDebugQueryLog(source);

        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
        {
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => DoToListAsync(source, cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.TryToMongoQueryString);
        }

        return await DoToListAsync(source, cancellationToken);

        static async Task<List<TSource>> DoToListAsync(
            IEnumerable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            if (source is IQueryable<TSource> queryable)
                return await queryable.ToListAsync(cancellationToken);

            return source.ToList();
        }
    }

    protected void LogDebugQueryLog<TSource>(IEnumerable<TSource> source)
    {
        if (Debugger.IsAttached && PersistenceConfiguration.EnableDebugQueryLog)
        {
            source.TryToMongoQueryString()
                .PipeAction(
                    queryStr =>
                    {
                        if (queryStr != null) Debugger.Log(0, null, queryStr + Environment.NewLine);
                    });
        }
    }

    public override async IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(
        IEnumerable<TSource> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using (var cursor = await source.As<IQueryable<TSource>>().ToCursorAsync(cancellationToken).ConfigureAwait(false))
        {
            Ensure.IsNotNull(cursor, nameof(source));
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var item in cursor.Current) yield return item;

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    public override async Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        LogDebugQueryLog(source);

        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
        {
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.FirstOrDefaultAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.TryToMongoQueryString);
        }

        return await source.FirstOrDefaultAsync(cancellationToken);
    }

    public override async Task<TSource> FirstOrDefaultAsync<TSource>(
        IEnumerable<TSource> query,
        CancellationToken cancellationToken = default)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        LogDebugQueryLog(query);

        if (query.As<IQueryable<TSource>>() != null)
            return await FirstOrDefaultAsync(query.As<IQueryable<TSource>>(), cancellationToken);

        // ReSharper disable once PossibleMultipleEnumeration
        return query.FirstOrDefault();
    }

    public override async Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        LogDebugQueryLog(source);

        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
        {
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.FirstAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.TryToMongoQueryString);
        }

        return await source.FirstAsync(cancellationToken);
    }

    public override async Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        LogDebugQueryLog(source);

        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
        {
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.CountAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.TryToMongoQueryString);
        }

        return await source.CountAsync(cancellationToken);
    }

    public override async Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        LogDebugQueryLog(source);

        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
        {
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.AnyAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.TryToMongoQueryString);
        }

        return await source.AnyAsync(cancellationToken);
    }

    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IPlatformUnitOfWork uow)
    {
        if (result is null) return false;

        return result.GetType().IsAssignableToGenericType(typeof(IQueryable<>)) ||
               result.GetType().IsAssignableToGenericType(typeof(IAsyncEnumerable<>));
    }

    protected override void HandleDisposeUsingOnceTransientUowLogic<TResult>(
        IPlatformUnitOfWork uow,
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
    public PlatformMongoDbRootRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
    }
}
