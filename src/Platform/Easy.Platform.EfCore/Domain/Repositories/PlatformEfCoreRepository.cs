using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public abstract class PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext> : PlatformRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }

    protected virtual TDbContext DbContext => GetUowDbContext(CurrentActiveUow());

    /// <summary>
    /// Gets DbSet for given entity.
    /// </summary>
    protected DbSet<TEntity> Table => DbContext.Set<TEntity>();

    public DbSet<TEntity> GetTable(IUnitOfWork uow)
    {
        return GetUowDbContext(uow).Set<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow)
    {
        return GetTable(uow).AsNoTracking().AsQueryable();
    }

    public override IQueryable<TEntity> GetReadonlyQuery(IUnitOfWork uow)
    {
        return GetTable(uow).AsNoTracking().AsQueryable();
    }

    public TDbContext GetUowDbContext(IUnitOfWork uow)
    {
        return FindDbContextUow<IPlatformEfCoreUnitOfWork<TDbContext>>(uow).DbContext;
    }

    public override IUnitOfWork CurrentActiveUow()
    {
        return UnitOfWorkManager.CurrentInnerActiveUow<IPlatformEfCoreUnitOfWork<TDbContext>>();
    }

    public override IUnitOfWork CurrentReadonlyDataEnumerableUow()
    {
        return UnitOfWorkManager.CurrentReadonlyDataEnumerableUow().FindFirstInnerUowOfType<IPlatformEfCoreUnitOfWork<TDbContext>>();
    }

    public override Task<TEntity> GetByIdAsync(TPrimaryKey id, CancellationToken cancellationToken = default)
    {
        return FirstOrDefaultAsync(p => p.Id.Equals(id), cancellationToken);
    }

    public override Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default)
    {
        return GetAllAsync(p => ids.Contains(p.Id), cancellationToken);
    }

    public override Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query.WhereIf(predicate != null, predicate).ToListAsync(cancellationToken));
    }

    public override Task<List<TEntity>> GetAllAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return query.ToListAsync(cancellationToken);
    }

    public override Task<TEntity> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
    {
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public override Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query
                .WhereIf(predicate != null, predicate)
                .FirstAsync(cancellationToken));
    }

    public override Task<TEntity> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query
                .WhereIf(predicate != null, predicate)
                .FirstOrDefaultAsync(cancellationToken));
    }

    public override Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query.WhereIf(predicate != null, predicate).CountAsync(cancellationToken));
    }

    public override Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return query.CountAsync(cancellationToken);
    }

    public override Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query.WhereIf(predicate != null, predicate).AnyAsync(cancellationToken));
    }

    public override Task<int> CountAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(query).CountAsync(cancellationToken));
    }

    public override Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(uow, query).CountAsync(cancellationToken));
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(query).ToListAsync(cancellationToken));
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(uow, query).ToListAsync(cancellationToken));
    }

    public override Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(query).FirstOrDefaultAsync(cancellationToken));
    }

    public override Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(uow, query).FirstOrDefaultAsync(cancellationToken));
    }

    public override async Task<TEntity> CreateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await CreateAsync(uow, entity, dismissSendEvent, cancellationToken));
    }

    public override Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return CreateOrUpdateAsync(
            entity,
            null,
            dismissSendEvent,
            cancellationToken);
    }

    public override Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow =>
            {
                var existingEntity = customCheckExistingPredicate != null
                    ? GetQuery(uow).AsNoTracking().FirstOrDefault(customCheckExistingPredicate)
                    : GetQuery(uow).AsNoTracking().FirstOrDefault(p => p.Id.Equals(entity.Id));

                if (existingEntity != null)
                {
                    entity.Id = existingEntity.Id;

                    if (entity is IRowVersionEntity rowVersionEntity &&
                        existingEntity is IRowVersionEntity existingRowVersionEntity)
                        rowVersionEntity.ConcurrencyUpdateToken = existingRowVersionEntity.ConcurrencyUpdateToken;

                    return UpdateAsync(uow, entity, dismissSendEvent, cancellationToken);
                }

                return CreateAsync(uow, entity, dismissSendEvent, cancellationToken);
            });
    }

    public override async Task<List<TEntity>> CreateOrUpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow =>
            {
                var entityIds = entities.Select(p => p.Id);

                var existingEntityIds = await GetQuery(uow)
                    .Where(p => entityIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .Then(_ => _.ToHashSet());

                var toCreateEntities = entities.Where(p => !existingEntityIds.Contains(p.Id)).ToList();
                var toUpdateEntities = entities.Where(p => existingEntityIds.Contains(p.Id)).ToList();

                await CreateManyAsync(uow, toCreateEntities, dismissSendEvent, cancellationToken);
                await UpdateManyAsync(uow, toUpdateEntities, dismissSendEvent, cancellationToken);

                return entities;
            });
    }

    public override async Task<TEntity> UpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await UpdateAsync(uow, entity, dismissSendEvent, cancellationToken));
    }

    public override Task DeleteAsync(
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow =>
            {
                var entity = await GetTable(uow).FindAsync(entityId);

                if (entity != null) await DeleteAsync(uow, entity, dismissSendEvent, cancellationToken);
            });
    }

    public override Task DeleteAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow =>
            {
                await DeleteAsync(uow, entity, dismissSendEvent, cancellationToken);
            });
    }

    public override async Task<List<TEntity>> CreateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await CreateManyAsync(uow, entities, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> UpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await UpdateManyAsync(uow, entities, dismissSendEvent, cancellationToken));
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow =>
            {
                var entities = await GetQuery(uow).Where(p => entityIds.Contains(p.Id)).ToListAsync(cancellationToken);

                return await DeleteManyAsync(uow, entities, dismissSendEvent, cancellationToken);
            });
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await DeleteManyAsync(uow, entities, dismissSendEvent, cancellationToken));
    }

    protected async Task<List<TEntity>> DeleteManyAsync(IUnitOfWork uow, List<TEntity> entities, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        GetTable(uow).RemoveRange(entities);

        if (!dismissSendEvent)
            await Cqrs.SendEvents(
                entities.Select(
                    entity => new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Deleted)),
                cancellationToken);

        return await entities.AsTask();
    }

    protected async Task<TEntity> UpdateAsync(IUnitOfWork uow, TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await EnsureEntityValid(entity, cancellationToken);

        var result = IsEntityTracked(uow, entity) ? entity : GetTable(uow).Update(entity).Entity;

        if (result is IRowVersionEntity rowVersionEntity)
            rowVersionEntity.ConcurrencyUpdateToken = Guid.NewGuid();

        if (!dismissSendEvent)
            await Cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Updated),
                cancellationToken);

        return result;
    }

    protected bool IsEntityTracked(IUnitOfWork uow, TEntity entity)
    {
        return GetTable(uow).Local.Any(e => e == entity);
    }

    protected async Task DeleteAsync(IUnitOfWork uow, TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await GetTable(uow).Remove(entity).Entity.AsTask();
        if (!dismissSendEvent)
            await Cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Deleted),
                cancellationToken);
    }

    protected async Task<TEntity> CreateAsync(IUnitOfWork uow, TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await EnsureEntityValid(entity, cancellationToken);

        var result = await GetTable(uow).AddAsync(entity, cancellationToken).AsTask().Then(p => entity);
        if (!dismissSendEvent)
            await Cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Created),
                cancellationToken);

        return result;
    }

    protected async Task<List<TEntity>> CreateManyAsync(IUnitOfWork uow, List<TEntity> entities, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await EnsureEntitiesValid(entities, cancellationToken);

        var result = await GetTable(uow).AddRangeAsync(entities, cancellationToken).Then(() => entities);

        if (!dismissSendEvent)
            await Cqrs.SendEvents(
                entities.Select(
                    entity => new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Created)),
                cancellationToken);

        return result;
    }

    protected async Task<List<TEntity>> UpdateManyAsync(IUnitOfWork uow, List<TEntity> entities, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await entities.ForEachAsync(entity => UpdateAsync(uow, entity, dismissSendEvent, cancellationToken));

        return entities;
    }
}
