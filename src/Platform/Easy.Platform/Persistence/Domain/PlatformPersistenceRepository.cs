using System.Linq.Expressions;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Exceptions.Extensions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Persistence.Domain;

public abstract class PlatformPersistenceRepository<TEntity, TPrimaryKey, TUow, TDbContext> : PlatformRepository<TEntity, TPrimaryKey, TUow>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TUow : class, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    protected PlatformPersistenceRepository(
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        IServiceProvider serviceProvider) : base(unitOfWorkManager, cqrs, serviceProvider)
    {
        PersistenceConfiguration = serviceProvider.GetService<PlatformPersistenceConfiguration<TDbContext>>();
        Logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
    }

    /// <summary>
    /// Return CurrentActiveUow db context if exist or. <br />
    /// Auto use GlobalUow if there's no current active uow. <br />
    /// Support for old system code or other application want to use platform repository inherit DbContext but without open new uow
    /// </summary>
    protected virtual TDbContext DbContext => GetUowDbContext(TryGetCurrentActiveUow() ?? UnitOfWorkManager.GlobalUow);

    protected PlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration { get; }

    protected ILogger Logger { get; }

    protected override async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, Task<TResult>> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                async () => await base.ExecuteAutoOpenUowUsingOnceTimeForRead(readDataFn, loadRelatedEntities),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false);

        return await base.ExecuteAutoOpenUowUsingOnceTimeForRead(readDataFn, loadRelatedEntities);
    }

    protected override async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForWrite<TResult>(Func<IUnitOfWork, Task<TResult>> action)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                async () => await base.ExecuteAutoOpenUowUsingOnceTimeForWrite(action),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: true);

        return await base.ExecuteAutoOpenUowUsingOnceTimeForWrite(action);
    }

    public TDbContext GetUowDbContext(IUnitOfWork uow)
    {
        return uow.UowOfType<TUow>().DbContext;
    }

    public virtual async Task<List<TSource>> ToListAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return await ToAsyncEnumerable(source, cancellationToken).ToListAsync(cancellationToken).AsTask();
    }

    /// <summary>
    /// Use ToAsyncEnumerable to convert IQueryable to IAsyncEnumerable to help return data like a stream. Also help
    /// using it as a true IEnumerable which Then can select anything and it will work.
    /// Default as Enumerable from IQueryable still like Queryable which cause error query could not be translated for free select using constructor map for example
    /// </summary>
    public abstract IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<TSource?> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<bool> AnyAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public override async Task<TEntity> GetByIdAsync(
        TPrimaryKey id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await FirstOrDefaultAsync(query.Where(p => p.Id.Equals(id)), cancellationToken)
                .EnsureFound($"{typeof(TEntity).Name} with Id {id} is not found"),
            loadRelatedEntities);
    }

    public override async Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await ToListAsync(query.Where(p => ids.Contains(p.Id)), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await ToListAsync(query.WhereIf(predicate != null, predicate), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<List<TEntity>> GetAllAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return await ToListAsync(query, cancellationToken);
    }

    public override IEnumerable<TEntity> GetAllEnumerable(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => ToAsyncEnumerable(query.WhereIf(predicate != null, predicate), cancellationToken).ToEnumerable(),
                loadRelatedEntities)
            .GetResult();
    }

    public override IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return ToAsyncEnumerable(query, cancellationToken);
    }

    public override IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => ToAsyncEnumerable(queryBuilder(query), cancellationToken),
                loadRelatedEntities)
            .GetResult();
    }

    public override IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => ToAsyncEnumerable(queryBuilder(uow, query), cancellationToken),
                loadRelatedEntities)
            .GetResult();
    }

    public override async Task<TEntity?> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
    {
        return await FirstOrDefaultAsync(query, cancellationToken);
    }

    public override async Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await FirstOrDefaultAsync(query.WhereIf(predicate != null, predicate), cancellationToken)
                .EnsureFound($"{typeof(TEntity).Name} is not found"),
            loadRelatedEntities);
    }

    public override async Task<TEntity> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await FirstOrDefaultAsync(query.WhereIf(predicate != null, predicate), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await CountAsync(query.WhereIf(predicate != null, predicate), cancellationToken),
            Array.Empty<Expression<Func<TEntity, object>>>());
    }

    public override async Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return await CountAsync(query, cancellationToken);
    }

    public override async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await AnyAsync(query.WhereIf(predicate != null, predicate), cancellationToken),
            Array.Empty<Expression<Func<TEntity, object>>>());
    }

    public override async Task<int> CountAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await CountAsync(queryBuilder(query), cancellationToken),
            Array.Empty<Expression<Func<TEntity, object>>>());
    }

    public override async Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await CountAsync(queryBuilder(uow, query), cancellationToken),
            Array.Empty<Expression<Func<TEntity, object>>>());
    }

    public override async Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await ToListAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await ToListAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await FirstOrDefaultAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await FirstOrDefaultAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<TEntity> CreateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken));
    }

    public override async Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, cancellationToken));
    }

    public override async Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, customCheckExistingPredicate, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> CreateOrUpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(entities, null, dismissSendEvent, cancellationToken));
    }

    public override async Task<TEntity> UpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken));
    }

    public override async Task DeleteAsync(
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteAsync<TEntity, TPrimaryKey>(entityId, dismissSendEvent, cancellationToken));
    }

    public override async Task DeleteAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> CreateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> UpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).UpdateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, cancellationToken));
    }

    public virtual async Task<List<TEntity>> UpdateManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).UpdateManyAsync<TEntity, TPrimaryKey>(predicate, updateAction, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entityIds, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(predicate, dismissSendEvent, cancellationToken));
    }
}
