using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Domain.Repositories;

public abstract class PlatformRepository<TEntity, TPrimaryKey> : IPlatformQueryableRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
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
    public abstract IUnitOfWork CurrentActiveUow();

    public abstract Task<TEntity> GetByIdAsync(TPrimaryKey id, CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public async Task<List<TEntity>> GetAllAsync(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetAllAsync(queryBuilder(uow, query), cancellationToken));
    }

    public abstract Task<List<TEntity>> GetAllAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default);

    public abstract Task<TEntity> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public abstract Task<TEntity> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public abstract Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public async Task<TResult> GetAsync<TResult>(Func<IQueryable<TEntity>, TResult> queryToResultBuilder, CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead((uow, query) => queryToResultBuilder(query));
    }

    public async Task<TResult> GetAsync<TResult>(Func<IQueryable<TEntity>, Task<TResult>> queryToResultBuilder, CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead((uow, query) => queryToResultBuilder(query));
    }

    public async Task<List<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetAllAsync(queryBuilder(query), cancellationToken));
    }

    public Task<TEntity> FirstOrDefaultAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => FirstOrDefaultAsync(queryBuilder(query), cancellationToken));
    }

    public Task<TEntity> FirstOrDefaultAsync(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => FirstOrDefaultAsync(queryBuilder(uow, query), cancellationToken));
    }

    public abstract Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default);

    public Func<IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> builderFn)
    {
        return builderFn;
    }

    public Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> builderFn)
    {
        return builderFn;
    }

    public Func<IQueryable<TEntity>, IQueryable<TEntity>> GetQueryBuilder(Expression<Func<TEntity, bool>> queryExpression)
    {
        return query => query.Where(queryExpression);
    }

    public abstract Task<int> CountAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public IEnumerable<TEntity> GetReadonlyDataEnumerable()
    {
        return GetReadonlyQuery(CurrentReadonlyDataEnumerableUow()).AsEnumerable();
    }

    public IEnumerable<TSelector> GetReadonlyDataEnumerable<TSelector>(Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder)
    {
        return GetReadonlyQuery(CurrentReadonlyDataEnumerableUow()).Pipe(queryBuilder).AsEnumerable();
    }

    public IEnumerable<TSelector> GetReadonlyDataEnumerable<TSelector>(Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder)
    {
        return GetReadonlyQuery(CurrentReadonlyDataEnumerableUow())
            .Pipe(query => queryBuilder(CurrentReadonlyDataEnumerableUow(), query))
            .AsEnumerable();
    }

    public abstract IQueryable<TEntity> GetQuery(IUnitOfWork uow);

    public abstract IUnitOfWork CurrentReadonlyDataEnumerableUow();

    public abstract IQueryable<TEntity> GetReadonlyQuery(IUnitOfWork uow);

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

    public abstract Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public virtual async Task<List<TEntity>> DeleteManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await DeleteManyAsync(
                await GetAllAsync(predicate, cancellationToken),
                dismissSendEvent,
                cancellationToken));
    }

    protected virtual async Task ExecuteAutoOpenUowUsingOnceTime(
        Func<IQueryable<TEntity>, Task> executeAsync)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
            using (var uow = UnitOfWorkManager.CreateNewUow())
            {
                await executeAsync(GetQuery(uow));
            }

        await executeAsync(GetQuery(UnitOfWorkManager.TryGetCurrentActiveUow()));
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, Task<TResult>> readDataFn)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
            using (var uow = UnitOfWorkManager.CreateNewUow())
            {
                return await readDataFn(uow, GetQuery(uow));
            }

        return await readDataFn(
            UnitOfWorkManager.TryGetCurrentActiveUow(),
            GetQuery(UnitOfWorkManager.TryGetCurrentActiveUow()));
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForRead<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, TResult> readDataFn)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
            using (var uow = UnitOfWorkManager.CreateNewUow())
            {
                return readDataFn(uow, GetQuery(uow));
            }

        return readDataFn(
            UnitOfWorkManager.TryGetCurrentActiveUow(),
            GetQuery(UnitOfWorkManager.TryGetCurrentActiveUow()));
    }

    protected virtual async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForWrite<TResult>(
        Func<IUnitOfWork, Task<TResult>> action)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
            return await ServiceProvider.ExecuteInjectScopedAsync<TResult>(
                async (IUnitOfWorkManager uowManager) =>
                {
                    using (var uow = UnitOfWorkManager.Begin())
                    {
                        var result = await action(uow);
                        await uow.CompleteAsync();
                        return result;
                    }
                });

        return await action(UnitOfWorkManager.CurrentActiveUow());
    }

    protected async Task ExecuteAutoOpenUowUsingOnceTimeForWrite(
        Func<IUnitOfWork, Task> action)
    {
        if (UnitOfWorkManager.TryGetCurrentActiveUow() == null)
            await ServiceProvider.ExecuteInjectScopedAsync(
                async (IUnitOfWorkManager uowManager) =>
                {
                    using (var uow = UnitOfWorkManager.Begin())
                    {
                        await action(uow);
                        await uow.CompleteAsync();
                    }
                });
        else await action(UnitOfWorkManager.CurrentActiveUow());
    }

    protected async Task EnsureEntityValid(TEntity entity, CancellationToken cancellationToken)
    {
        if (entity is IValidatableEntity<TEntity, TPrimaryKey> validatableEntity)
        {
            validatableEntity.Validate().EnsureValid();

            await EnsureValid(
                validatableEntity.CheckUniquenessValidator()
                    ?.Validate(predicate => AnyAsync(predicate, cancellationToken)));
        }
    }

    protected async Task EnsureEntitiesValid(List<TEntity> entities, CancellationToken cancellationToken)
    {
        EnsureValid(entities);
        await EnsureEntitiesUniqueness(entities, cancellationToken);
    }

    protected void EnsureValid(List<TEntity> entities)
    {
        entities.ForEach(
            entity =>
            {
                if (entity is IValidatableEntity<TEntity, TPrimaryKey> validatableEntity)
                    EnsureValid(validatableEntity.Validate());
            });
    }

    protected void EnsureValid(PlatformValidationResult validationResult)
    {
        if (validationResult != null && !validationResult.IsValid)
            throw new PlatformDomainValidationException(validationResult);
    }

    protected void EnsureValid(List<Func<PlatformValidationResult>> validationResultFns)
    {
        validationResultFns.ForEach(
            validationResultFn =>
            {
                var validationResult = validationResultFn();
                if (validationResult?.IsValid == false)
                    throw new PlatformDomainValidationException(validationResult);
            });
    }

    protected async Task EnsureValid(Task<PlatformValidationResult> validationResultTask)
    {
        if (validationResultTask == null)
            return;

        var validationResult = await validationResultTask;
        if (validationResult?.IsValid == false)
            throw new PlatformDomainValidationException(validationResult);
    }

    protected async Task EnsureValid(List<Func<Task<PlatformValidationResult>>> validationResultAsyncFns)
    {
        await validationResultAsyncFns.ForEachAsync(
            async validationResultAsyncFn =>
            {
                var validationResult = await validationResultAsyncFn();
                if (validationResult?.IsValid == false)
                    throw new PlatformDomainValidationException(validationResult);
            });
    }

    protected async Task EnsureEntitiesUniqueness(List<TEntity> entities, CancellationToken cancellationToken)
    {
        // Validate each IValidatableEntity with CheckUniquenessValidator != null must be unique in the existing in database items
        // and also in the list items itself
        var entitiesValidateUniquenessFns = entities
            .Where(
                entity => entity is IValidatableEntity<TEntity, TPrimaryKey> validatableEntity &&
                          validatableEntity.CheckUniquenessValidator() != null)
            .Select(p => (IValidatableEntity<TEntity, TPrimaryKey>)p)
            .Select<IValidatableEntity<TEntity, TPrimaryKey>, Func<Task<PlatformValidationResult>>>(
                entity =>
                    () => entity.CheckUniquenessValidator()
                        .Validate(
                            checkAnyDuplicatedItemAsyncFunction: async findOtherDuplicatedItemPredicate =>
                                entities.Any(findOtherDuplicatedItemPredicate.Compile()) ||
                                await AnyAsync(findOtherDuplicatedItemPredicate, cancellationToken)))
            .ToList();
        await EnsureValid(entitiesValidateUniquenessFns);
    }

    protected TUow FindDbContextUow<TUow>(IUnitOfWork uow) where TUow : class, IUnitOfWork
    {
        return uow?.As<TUow>() ??
               uow?.CurrentInner<TUow>();
    }
}
