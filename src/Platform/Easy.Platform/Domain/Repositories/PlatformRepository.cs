using System.Linq.Expressions;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Domain.Repositories;

public abstract class PlatformRepository<TEntity, TPrimaryKey, TUow> : IPlatformQueryableRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TUow : class, IPlatformUnitOfWork
{
    private readonly Lazy<IPlatformCqrs> cqrsLazy;

    public PlatformRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
        cqrsLazy = new Lazy<IPlatformCqrs>(() => serviceProvider.GetRequiredService<IPlatformCqrs>());
        ServiceProvider = serviceProvider;
        RootServiceProvider = serviceProvider.GetRequiredService<IPlatformRootServiceProvider>();
        IsDistributedTracingEnabled = serviceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
    }

    protected IPlatformRootServiceProvider RootServiceProvider { get; }
    protected virtual bool IsDistributedTracingEnabled { get; }
    public IPlatformUnitOfWorkManager UnitOfWorkManager { get; }
    protected IPlatformCqrs Cqrs => cqrsLazy.Value;
    protected IServiceProvider ServiceProvider { get; }

    public IPlatformUnitOfWork CurrentActiveUow()
    {
        return UnitOfWorkManager.CurrentActiveUow().UowOfType<TUow>();
    }

    public IPlatformUnitOfWork CurrentOrCreatedActiveUow(string uowId)
    {
        return UnitOfWorkManager.CurrentOrCreatedActiveUow(uowId).UowOfType<TUow>();
    }

    public IPlatformUnitOfWorkManager UowManager()
    {
        return UnitOfWorkManager;
    }

    public abstract Task<TEntity> GetByIdAsync(
        TPrimaryKey id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public virtual Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => GetAllAsync(query.WhereIf(predicate != null, predicate), cancellationToken),
            loadRelatedEntities);
    }

    public Task<List<TEntity>> GetAllAsync(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetAllAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public abstract Task<List<TEntity>> GetAllAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public virtual IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (_, query) => GetAllAsyncEnumerable(queryBuilder(query), cancellationToken),
                loadRelatedEntities)
            .GetResult();
    }

    public virtual IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => GetAllAsyncEnumerable(queryBuilder(uow, query), cancellationToken),
                loadRelatedEntities)
            .GetResult();
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

    public Task<TEntity> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
    {
        return FirstOrDefaultAsync(query.As<IEnumerable<TEntity>>(), cancellationToken);
    }

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

    public Task<List<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => GetAllAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public Task<TEntity> FirstOrDefaultAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => FirstOrDefaultAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public Task<TEntity> FirstOrDefaultAsync(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
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
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public abstract Task<int> CountAsync<TQueryItemResult>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
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

    public Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> builderFn)
    {
        return builderFn;
    }

    public Func<IQueryable<TEntity>, IQueryable<TEntity>> GetQueryBuilder(Expression<Func<TEntity, bool>> predicate)
    {
        return query => query.Where(predicate);
    }

    public abstract IQueryable<TEntity> GetQuery(IPlatformUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public IQueryable<TEntity> GetCurrentUowQuery(params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetQuery(CurrentActiveUow(), loadRelatedEntities);
    }

    public abstract Task<TSource> FirstOrDefaultAsync<TSource>(
        IEnumerable<TSource> query,
        CancellationToken cancellationToken = default);

    public async Task<List<TEntity>> UpdateManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        var updateItems = await GetAllAsync(predicate, cancellationToken)
            .ThenAction(items => items.ForEach(updateAction));

        return await UpdateManyAsync(updateItems, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        var toDeleteEntities = await GetAllAsync(predicate, cancellationToken);

        return await DeleteManyAsync(toDeleteEntities, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public IPlatformUnitOfWork TryGetCurrentActiveUow()
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
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> CreateAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> CreateOrUpdateAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> CreateOrUpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> CreateOrUpdateManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> UpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> UpdateAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> DeleteAsync(
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> DeleteAsync(
        IPlatformUnitOfWork uow,
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> DeleteAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> CreateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> CreateManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> UpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> UpdateManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    protected abstract void HandleDisposeUsingOnceTimeContextLogic<TResult>(
        IPlatformUnitOfWork uow,
        bool doesNeedKeepUowForQueryOrEnumerableExecutionLater,
        Expression<Func<TEntity, object>>[] loadRelatedEntities,
        TResult result);

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, Task<TResult>> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
        {
            var uow = UnitOfWorkManager.CreateNewUow(true);
            TResult result = default;

            try
            {
                result = await ExecuteReadData(uow, readDataFn, loadRelatedEntities);

                return result;
            }
            finally
            {
                HandleDisposeUsingOnceTimeContextLogic(uow, DoesNeedKeepUowForQueryOrEnumerableExecutionLater(result, uow), loadRelatedEntities, result);
            }
        }

        return await ExecuteUowThreadSafe(UnitOfWorkManager.CurrentActiveUow(), uow => ExecuteReadData(uow, readDataFn, loadRelatedEntities));
    }

    protected async Task<TResult> ExecuteUowThreadSafe<TResult>(IPlatformUnitOfWork uow, Func<IPlatformUnitOfWork, Task<TResult>> executeFn)
    {
        // Do retry if the uow do not support parallel query so that if there's other uow running query in parallel, it could retry get data again to have chance to make it work
        if (uow.UowOfType<TUow>().DoesSupportParallelQuery() == false)
            try
            {
                //Asynchronously wait to enter the Semaphore. If no-one has been granted access to the Semaphore, code execution will proceed, otherwise this thread waits here until the semaphore is released 
                await uow.UowOfType<TUow>().LockAsync();

                return await executeFn(uow);
            }
            finally
            {
                //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
                //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
                uow.UowOfType<TUow>().ReleaseLock();
            }

        return await executeFn(uow);
    }

    protected Task<TResult> ExecuteReadData<TResult>(
        IPlatformUnitOfWork uow,
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, Task<TResult>> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return readDataFn(uow, GetQuery(uow, loadRelatedEntities));
    }

    protected virtual Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, TResult> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(ReadDataFnAsync, loadRelatedEntities);

        Task<TResult> ReadDataFnAsync(IPlatformUnitOfWork unitOfWork, IQueryable<TEntity> entities)
        {
            return readDataFn(unitOfWork, entities).BoxedInTask();
        }
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForWrite<TResult>(
        Func<IPlatformUnitOfWork, Task<TResult>> action,
        IPlatformUnitOfWork forceUseUow = null)
    {
        if (forceUseUow != null) return await ExecuteWriteData(action, forceUseUow);

        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
        {
            var uow = UnitOfWorkManager.CreateNewUow(true);
            TResult result = default;

            try
            {
                result = await ExecuteWriteData(action, uow);

                await uow.CompleteAsync();

                return result;
            }
            finally
            {
                if (!DoesNeedKeepUowForQueryOrEnumerableExecutionLater(result, uow)) uow.Dispose();
            }
        }

        return await ExecuteWriteData(action, UnitOfWorkManager.CurrentActiveUow());
    }

    private static async Task<TResult> ExecuteWriteData<TResult>(Func<IPlatformUnitOfWork, Task<TResult>> action, IPlatformUnitOfWork uow)
    {
        var result = await action(uow);
        return result;
    }

    protected abstract bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IPlatformUnitOfWork uow);
}
