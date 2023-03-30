using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Domain.Repositories;

public abstract class PlatformRepository<TEntity, TPrimaryKey, TUow> : IPlatformQueryableRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TUow : class, IUnitOfWork
{
    public PlatformRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Cqrs = cqrs;
        ServiceProvider = serviceProvider;
    }

    public IUnitOfWorkManager UnitOfWorkManager { get; }
    protected IPlatformCqrs Cqrs { get; }
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Return current active uow. May throw exception if not existing one.
    /// </summary>
    public IUnitOfWork CurrentActiveUow()
    {
        return UnitOfWorkManager.CurrentActiveUow().UowOfType<TUow>();
    }

    public abstract Task<TEntity> GetByIdAsync(
        TPrimaryKey id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public async Task<List<TEntity>> GetAllAsync(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetAllAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public abstract Task<List<TEntity>> GetAllAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => GetAllAsyncEnumerable(queryBuilder(query), cancellationToken),
                loadRelatedEntities)
            .Result;
    }

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => GetAllAsyncEnumerable(queryBuilder(uow, query), cancellationToken),
                loadRelatedEntities)
            .Result;
    }

    public abstract IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(IQueryable<TEntity> query, CancellationToken cancellationToken = default);

    public abstract Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<TEntity> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<TEntity> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public abstract Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public abstract IEnumerable<TEntity> GetAllEnumerable(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public async Task<TResult> GetAsync<TResult>(
        Func<IQueryable<TEntity>, TResult> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead((uow, query) => queryBuilder(query), loadRelatedEntities);
    }

    public async Task<TResult> GetAsync<TResult>(
        Func<IQueryable<TEntity>, Task<TResult>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead((uow, query) => queryBuilder(query), loadRelatedEntities);
    }

    public async Task<TResult> GetAsync<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, TResult> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead((uow, query) => queryBuilder(uow, query), loadRelatedEntities);
    }

    public async Task<TResult> GetAsync<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, Task<TResult>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead((uow, query) => queryBuilder(uow, query), loadRelatedEntities);
    }

    public async Task<List<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetAllAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public Task<TEntity> FirstOrDefaultAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => FirstOrDefaultAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public Task<TEntity> FirstOrDefaultAsync(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => FirstOrDefaultAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public abstract Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public IQueryable<TEntity> GetGlobalUowQuery(params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetQuery(GlobalUow(), loadRelatedEntities);
    }

    public Func<IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> builderFn)
    {
        return builderFn;
    }

    public Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> builderFn)
    {
        return builderFn;
    }

    public Func<IQueryable<TEntity>, IQueryable<TEntity>> GetQueryBuilder(Expression<Func<TEntity, bool>> predicate)
    {
        return query => query.Where(predicate);
    }

    public abstract IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public IQueryable<TEntity> GetCurrentUowQuery(params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetQuery(CurrentActiveUow(), loadRelatedEntities);
    }

    public IUnitOfWork TryGetCurrentActiveUow()
    {
        return UnitOfWorkManager.TryGetCurrentActiveUow()?.UowOfType<TUow>();
    }

    public TUow GlobalUow()
    {
        return UnitOfWorkManager.GlobalUow.UowOfType<TUow>();
    }

    public abstract Task<TEntity> CreateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> CreateOrUpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> UpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task DeleteAsync(
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task DeleteAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> CreateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> UpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, Task<TResult>> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
        {
            var uow = UnitOfWorkManager.CreateNewUow();

            var result = await readDataFn(uow, GetQuery(uow, loadRelatedEntities));

            if (!DoesNeedKeepUowForQueryOrEnumerableExecutionLater(result))
                uow.Dispose();

            return result;
        }

        return await readDataFn(
            UnitOfWorkManager.CurrentActiveUow(),
            GetQuery(UnitOfWorkManager.CurrentActiveUow(), loadRelatedEntities));
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, TResult> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(ReadDataFnAsync, loadRelatedEntities);

        Task<TResult> ReadDataFnAsync(IUnitOfWork unitOfWork, IQueryable<TEntity> entities)
        {
            return readDataFn(unitOfWork, entities).AsTask();
        }
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForWrite<TResult>(
        Func<IUnitOfWork, Task<TResult>> action)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
        {
            var uow = UnitOfWorkManager.CreateNewUow();

            var result = await action(uow);
            await uow.CompleteAsync();

            if (!DoesNeedKeepUowForQueryOrEnumerableExecutionLater(result)) uow.Dispose();

            return result;
        }

        return await action(UnitOfWorkManager.CurrentActiveUow());
    }

    protected async Task ExecuteAutoOpenUowUsingOnceTimeForWrite(
        Func<IUnitOfWork, Task> action)
    {
        await ExecuteAutoOpenUowUsingOnceTimeForWrite(ActionWithResult);

        async Task<object> ActionWithResult(IUnitOfWork unitOfWork)
        {
            await action(unitOfWork);

            return null;
        }
    }

    private static bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result)
    {
        return result != null &&
               (result.GetType().IsAssignableToGenericType(typeof(IQueryable<>)) ||
                result.GetType().IsAssignableToGenericType(typeof(IAsyncEnumerable<>)) ||
                (result.GetType().IsAssignableToGenericType(typeof(IEnumerable<>)) &&
                 !(result.GetType().IsAssignableToGenericType(typeof(IList<>)) ||
                   result.GetType().IsArray ||
                   result.GetType().IsAssignableToGenericType(typeof(IDictionary<,>)) ||
                   result.GetType().IsAssignableToGenericType(typeof(ISet<>)))));
    }
}
