using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Domain.Repositories;

/// <summary>
/// This interface is used for conventional register class mapping via PlatformPersistenceModule.InternalRegister
/// </summary>
public interface IPlatformRepository
{
    public IUnitOfWork CurrentActiveUow();
}

public interface IPlatformBasicRepository<TEntity, TPrimaryKey> : IPlatformRepository
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    public Task<TEntity> GetByIdAsync(TPrimaryKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);
}

public interface IPlatformRepository<TEntity, TPrimaryKey> : IPlatformBasicRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    public Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public IEnumerable<TEntity> GetAllEnumerable(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);
}

public interface IPlatformBasicRootRepository<TEntity, TPrimaryKey> : IPlatformBasicRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
{
    public Task<TEntity> CreateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> CreateOrUpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<TEntity> UpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task DeleteAsync(
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task DeleteAsync(TEntity entity, bool dismissSendEvent = false, CancellationToken cancellationToken = default);

    public Task<List<TEntity>> CreateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> UpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> UpdateManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);
}

public interface IPlatformRootRepository<TEntity, TPrimaryKey>
    : IPlatformBasicRootRepository<TEntity, TPrimaryKey>, IPlatformRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
{
    public Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default);

    public async Task<TEntity> CreateIfNotExistAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        var isExisted = await AnyAsync(predicate, cancellationToken: cancellationToken);
        if (isExisted)
            return entity;

        return await CreateAsync(entity, dismissSendEvent, cancellationToken);
    }
}

public interface IPlatformQueryableRepository<TEntity, TPrimaryKey> : IPlatformRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    /// <summary>
    /// Return enumerable from query belong to <see cref="IUnitOfWorkManager.GlobalUow" />. <br />
    /// A single separated global uow in current scoped is used by repository for read data using query, usually when need to return data
    /// as enumerable to help download data like streaming data (not load all big data into ram) <br />
    /// or any other purpose that just want to using query directly without affecting the current active uow. <br />
    /// This uow is auto created once per scope when access it. <br />
    /// This won't affect the normal current uow queue list when Begin a new uow.
    /// </summary>
    public IQueryable<TEntity> GetGlobalUowQuery(params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    /// <summary>
    /// Return query of <see cref="IUnitOfWorkManager.CurrentActiveUow" />
    /// </summary>
    public IQueryable<TEntity> GetCurrentUowQuery(params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    /// <see cref="GetCurrentUowQuery" />
    public IQueryable<TResult> GetCurrentUowQuery<TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return queryBuilder(GetCurrentUowQuery(loadRelatedEntities));
    }

    /// <summary>
    /// Build and get query from a given uow
    /// </summary>
    public IQueryable<TResult> GetQuery<TResult>(
        IUnitOfWork uow,
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return queryBuilder(uow, GetQuery(uow, loadRelatedEntities));
    }

    /// <summary>
    /// Get query from a given uow
    /// </summary>
    public IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TResult> GetAsync<TResult>(
        Func<IQueryable<TEntity>, TResult> resultBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TResult> GetAsync<TResult>(
        Func<IQueryable<TEntity>, Task<TResult>> resultBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TResult> GetAsync<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, TResult> resultBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TResult> GetAsync<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, Task<TResult>> resultBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public TResult Get<TResult>(
        Func<IQueryable<TEntity>, TResult> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetAsync(queryBuilder, cancellationToken, loadRelatedEntities).GetResult();
    }

    public TResult Get<TResult>(
        Func<IQueryable<TEntity>, Task<TResult>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetAsync(queryBuilder, cancellationToken, loadRelatedEntities).GetResult();
    }

    public TResult Get<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, TResult> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetAsync(queryBuilder, cancellationToken, loadRelatedEntities).GetResult();
    }

    public TResult Get<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, Task<TResult>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetAsync(queryBuilder, cancellationToken, loadRelatedEntities).GetResult();
    }

    public Task<List<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetAllAsync(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetAllAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public Task<TEntity?> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public Task<TEntity?> FirstOrDefaultAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TEntity?> FirstOrDefaultAsync(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TSelector?> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<TSelector?> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities);

    public Task<int> CountAsync<TQueryItemResult>(Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder, CancellationToken cancellationToken = default);

    public Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Help to create a function to return a query which use want to use in the queryBuilder for a lot of
    /// other function. It's important to support PARALLELS query. If use GetAllQuery() to use outside, we need to open a uow without close,
    /// which could not run parallels because db context is not thread safe. <br />
    /// Ex:
    /// <br />
    /// var fullItemsQueryBuilder = repository.GetQueryBuilder(query => query.Where());<br />
    /// var pagedEntities = await repository.GetAllAsync(queryBuilder: query =>
    /// fullItemsQueryBuilder(query).PageBy(request.SkipCount, request.MaxResultCount));<br />
    /// var totalCount = await repository.CountAsync(fullItemsQueryBuilder, cancellationToken);
    /// </summary>
    public Func<IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> builderFn);

    /// <summary>
    /// Help to create a function to return a query which use want to use in the queryBuilder for a lot of
    /// other function. It's important to support PARALLELS query. If use GetAllQuery() to use outside, we need to open a uow without close,
    /// which could not run parallels because db context is not thread safe. <br />
    /// Ex:
    /// <br />
    /// var fullItemsQueryBuilder = repository.GetQueryBuilder((uow, query) => query.Where());<br />
    /// var pagedEntities = await repository.GetAllAsync(queryBuilder: (uow, query) =>
    /// fullItemsQueryBuilder(query).PageBy(request.SkipCount, request.MaxResultCount));<br />
    /// var totalCount = await repository.CountAsync(fullItemsQueryBuilder, cancellationToken);
    /// </summary>
    public Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> builderFn);

    /// <summary>
    /// Help to create a function to return a query which use want to use in the queryBuilder for a lot of
    /// other function. It's important to support PARALLELS query. If use GetAllQuery() to use outside, we need to open a uow without close,
    /// which could not run parallels because db context is not thread safe. <br />
    /// Ex:
    /// <br />
    /// var fullItemsQueryBuilder = repository.GetQueryBuilder(p => p.PropertyX == true);<br />
    /// var pagedEntities = await repository.GetAllAsync(queryBuilder: query =>
    /// fullItemsQueryBuilder(query).PageBy(request.SkipCount, request.MaxResultCount));<br />
    /// var totalCount = await repository.CountAsync(fullItemsQueryBuilder, cancellationToken);
    /// </summary>
    public Func<IQueryable<TEntity>, IQueryable<TEntity>> GetQueryBuilder(Expression<Func<TEntity, bool>> predicate);
}

public interface IPlatformQueryableRootRepository<TEntity, TPrimaryKey>
    : IPlatformQueryableRepository<TEntity, TPrimaryKey>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
{
}
