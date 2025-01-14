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
    private readonly Lazy<IPlatformUnitOfWorkManager> lazyUnitOfWorkManager;

    public PlatformRepository(IServiceProvider serviceProvider)
    {
        lazyUnitOfWorkManager = new Lazy<IPlatformUnitOfWorkManager>(() => serviceProvider.GetRequiredService<IPlatformUnitOfWorkManager>());
        cqrsLazy = serviceProvider.GetRequiredService<Lazy<IPlatformCqrs>>();
        ServiceProvider = serviceProvider;
        RootServiceProvider = serviceProvider.GetRequiredService<IPlatformRootServiceProvider>();
        IsDistributedTracingEnabled = serviceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
    }

    protected IPlatformRootServiceProvider RootServiceProvider { get; }
    protected virtual bool IsDistributedTracingEnabled { get; }
    public IPlatformUnitOfWorkManager UnitOfWorkManager => lazyUnitOfWorkManager.Value;
    protected IPlatformCqrs Cqrs => cqrsLazy.Value;
    protected IServiceProvider ServiceProvider { get; }

    protected IPlatformUnitOfWork GlobalUow => DoesSupportSingletonUow() && UnitOfWorkManager.GlobalSingletonUow != null
        ? UnitOfWorkManager.GlobalSingletonUow.Value
        : UnitOfWorkManager.GlobalScopedUow;

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

    public abstract Task<bool> AnyAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract IEnumerable<TEntity> GetAllEnumerable(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public void SetCachedOriginalEntitiesInUowForTrackingCompareAfterUpdate<TResult>(TResult? result, TUow uow)
    {
        if (uow == null)
            return;

        if (result is TEntity resultSingleEntity)
            uow.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(resultSingleEntity);
        else if (result is ICollection<TEntity> resultMultipleEntities && resultMultipleEntities.Any())
            resultMultipleEntities.ForEach(p => uow.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p));
        else if (result is ICollection<KeyValuePair<TPrimaryKey, TEntity>> resultMultipleEntitiesDict && resultMultipleEntitiesDict.Any())
            resultMultipleEntitiesDict.ForEach(p => uow.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p.Value));
        else if (result?.GetType().IsAnonymousType() == true)
        {
            foreach (var property in result.GetType().GetProperties())
                SetCachedOriginalEntitiesInUowForTrackingCompareAfterUpdate(property.GetValue(result), uow);
        }
    }

    public void SetCachedOriginalEntitiesInUowForTrackingCompareAfterUpdate<TResult>(TResult? result, IPlatformUnitOfWork uow)
    {
        SetCachedOriginalEntitiesInUowForTrackingCompareAfterUpdate(result, uow.UowOfType<TUow>());
    }

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

    public IPlatformRootServiceProvider GetRootServiceProvider()
    {
        return RootServiceProvider;
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

    public IPlatformUnitOfWork TryGetCurrentActiveUow()
    {
        return UnitOfWorkManager.TryGetCurrentActiveUow()?.UowOfType<TUow>();
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

    public abstract Task<TEntity> CreateOrUpdateAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
        TEntity? existingEntity,
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

    public abstract Task<TEntity> SetAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> SetAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
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

    public abstract Task<List<TPrimaryKey>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<int> DeleteManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<int> DeleteManyAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<int> DeleteManyAsync(
        IPlatformUnitOfWork uow,
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public abstract Task<int> DeleteManyAsync(
        IPlatformUnitOfWork uow,
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
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

    protected abstract void HandleDisposeUsingOnceTransientUowLogic<TResult>(
        IPlatformUnitOfWork uow,
        Expression<Func<TEntity, object>>[] loadRelatedEntities,
        TResult result);

    /// <summary>
    /// Return True to determine that this uow is Thread Safe and could support multiple parallel query
    /// </summary>
    protected abstract bool DoesSupportParallelExecution();

    protected abstract bool DoesSupportSingletonUow();

    /// <summary>
    /// If true, the uow actually do not handle real transaction. Repository when create/update data actually save immediately
    /// </summary>
    /// <remarks>
    /// The IsPseudoTransactionUow method is part of the IUnitOfWork interface in the Easy.Platform.Domain.UnitOfWork namespace. This method is used to determine whether the current unit of work (UoW) is handling a real transaction or not.
    /// <br />
    /// In the context of a UoW pattern, a real transaction implies that the changes made within the UoW are not immediately saved to the database, but are instead held until the UoW is completed. If the UoW fails, the changes can be rolled back, maintaining the integrity of the data.
    /// <br />
    /// On the other hand, a pseudo-transaction UoW implies that the changes are immediately saved to the database when they are made. This means there is no rollback mechanism if the UoW fails.
    /// <br />
    /// In the provided code, different implementations of the IUnitOfWork interface override the IsPseudoTransactionUow method to specify whether they handle real transactions or pseudo-transactions. For example, the PlatformEfCorePersistenceUnitOfWork class returns false, indicating it handles real transactions, while the PlatformMongoDbPersistenceUnitOfWork class returns true, indicating it handles pseudo-transactions.
    /// <br />
    /// This method is used in various parts of the code to decide how to handle certain operations. For example, in the PlatformCqrsEventApplicationHandler class, the IsPseudoTransactionUow method is used to determine whether to execute certain actions immediately or add them to the OnSaveChangesCompletedActions list to be executed when the UoW is completed.
    /// </remarks>
    protected abstract bool IsPseudoTransactionUow();

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, Task<TResult>> readDataFn,
        Expression<Func<TEntity, object>>[] loadRelatedEntities,
        bool forceOpenUowUsingOnce = false)
    {
        var currentActiveUow = forceOpenUowUsingOnce ? null : UnitOfWorkManager.TryGetCurrentActiveUow();

        if (currentActiveUow == null)
        {
            if (DoesSupportParallelExecution())
                return await ExecuteReadData(GlobalUow, readDataFn, loadRelatedEntities);
            else
            {
                var useOnceTransientUow = UnitOfWorkManager.CreateNewUow(true);
                TResult useOnceTransientUowResult = default;

                try
                {
                    useOnceTransientUowResult = await ExecuteReadData(useOnceTransientUow, readDataFn, loadRelatedEntities);

                    return useOnceTransientUowResult;
                }
                finally
                {
                    HandleDisposeUsingOnceTransientUowLogic(useOnceTransientUow, loadRelatedEntities, useOnceTransientUowResult);
                }
            }
        }
        else
        {
            var result = await ExecuteUowReadQueryThreadSafe(currentActiveUow, uow => ExecuteReadData(uow, readDataFn, loadRelatedEntities));

            // If there is opening uow, may get data for update => set cached original entities for track update
            SetCachedOriginalEntitiesInUowForTrackingCompareAfterUpdate(result, currentActiveUow.UowOfType<TUow>());

            return result;
        }
    }

    protected async Task<TResult> ExecuteUowReadQueryThreadSafe<TResult>(IPlatformUnitOfWork uow, Func<IPlatformUnitOfWork, Task<TResult>> executeFn)
    {
        if (DoesSupportParallelExecution() == false)
        {
            var uowOfTUow = uow.UowOfType<TUow>();

            try
            {
                //Asynchronously wait to enter the Semaphore. If no-one has been granted access to the Semaphore, code execution will proceed, otherwise this thread waits here until the semaphore is released 
                await uowOfTUow.LockAsync();

                var result = await executeFn(uow);

                uowOfTUow.ReleaseLock();

                return result;
            }
            catch (Exception)
            {
                //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
                //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
                uowOfTUow.ReleaseLock();
            }
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
        Expression<Func<TEntity, object>>[] loadRelatedEntities,
        bool forceOpenUowUsingOnce = false)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(ReadDataFnAsync, loadRelatedEntities, forceOpenUowUsingOnce);

        async Task<TResult> ReadDataFnAsync(IPlatformUnitOfWork unitOfWork, IQueryable<TEntity> entities)
        {
            return readDataFn(unitOfWork, entities);
        }
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForWrite<TResult>(
        Func<IPlatformUnitOfWork, Task<TResult>> action,
        IPlatformUnitOfWork forceUseUow = null)
    {
        if (forceUseUow != null) return await action(forceUseUow);

        var currentActiveUow = UnitOfWorkManager.TryGetCurrentActiveUow();

        if (currentActiveUow == null)
        {
            if (DoesSupportParallelExecution() && IsPseudoTransactionUow())
            {
                var result = await action(GlobalUow);

                return result;
            }
            else
            {
                var uow = UnitOfWorkManager.CreateNewUow(true);
                TResult result = default;

                try
                {
                    result = await action(uow);

                    await uow.CompleteAsync();

                    return result;
                }
                finally
                {
                    if (!DoesNeedKeepUowForQueryOrEnumerableExecutionLater(result, uow)) uow.Dispose();
                }
            }
        }
        else
            return await action(currentActiveUow);
    }

    protected abstract bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IPlatformUnitOfWork uow);
}
