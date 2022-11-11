using System.Linq.Expressions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.UnitOfWork;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public abstract class PlatformMongoDbRepository<TEntity, TPrimaryKey, TDbContext> : PlatformRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TDbContext : IPlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }

    protected virtual TDbContext DbContext => GetDbContext(CurrentActiveUow());

    protected virtual IMongoCollection<TEntity> Table => DbContext.GetCollection<TEntity>();

    public IMongoCollection<TEntity> GetTable(IUnitOfWork uow)
    {
        return GetDbContext(uow).GetCollection<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow)
    {
        return GetDbContext(uow).GetCollection<TEntity>().AsQueryable();
    }

    public override IQueryable<TEntity> GetReadonlyQuery(IUnitOfWork uow)
    {
        return GetDbContext(uow).GetCollection<TEntity>().AsQueryable();
    }

    public TDbContext GetDbContext(IUnitOfWork uow)
    {
        return FindDbContextUow<IPlatformMongoDbUnitOfWork<TDbContext>>(uow).DbContext;
    }

    public override IUnitOfWork CurrentActiveUow()
    {
        return UnitOfWorkManager.Begin(suppressCurrentUow: false).CurrentInner<IPlatformMongoDbUnitOfWork<TDbContext>>();
    }

    public override IUnitOfWork CurrentReadonlyDataEnumerableUow()
    {
        return UnitOfWorkManager.CurrentReadonlyDataEnumerableUow().FindFirstInnerUowOfType<IPlatformMongoDbUnitOfWork<TDbContext>>();
    }

    public override Task<List<TEntity>> GetAllAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return query
            .As<IMongoQueryable<TEntity>>()
            .ToListAsync(cancellationToken);
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
            (uow, query) => query.WhereIf(predicate != null, predicate).As<IMongoQueryable<TEntity>>().ToListAsync(cancellationToken));
    }

    public override Task<TEntity> FirstOrDefaultAsync(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
    {
        return query
            .As<IMongoQueryable<TEntity>>()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public override Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query
                .PipeIf(predicate != null, _ => _.Where(predicate!))
                .As<IMongoQueryable<TEntity>>()
                .FirstAsync(cancellationToken));
    }

    public override Task<TEntity> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query
                .PipeIf(predicate != null, _ => _.Where(predicate!))
                .As<IMongoQueryable<TEntity>>()
                .FirstOrDefaultAsync(cancellationToken));
    }

    public override async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            async (uow, query) => await query.WhereIf(predicate != null, predicate).As<IMongoQueryable<TEntity>>().CountAsync(cancellationToken));
    }

    public override Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return query
            .As<IMongoQueryable<TEntity>>()
            .CountAsync(cancellationToken);
    }

    public override Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => query.WhereIf(predicate != null, predicate).As<IMongoQueryable<TEntity>>().AnyAsync(cancellationToken));
    }

    public override Task<int> CountAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(query).As<IMongoQueryable<TQueryItemResult>>().CountAsync(cancellationToken));
    }

    public override Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(uow, query).As<IMongoQueryable<TQueryItemResult>>().CountAsync(cancellationToken));
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(query).As<IMongoQueryable<TSelector>>().ToListAsync(cancellationToken));
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(uow, query).As<IMongoQueryable<TSelector>>().ToListAsync(cancellationToken));
    }

    public override Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(query).As<IMongoQueryable<TSelector>>().FirstOrDefaultAsync(cancellationToken));
    }

    public override Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => queryBuilder(uow, query).As<IMongoQueryable<TSelector>>().FirstOrDefaultAsync(cancellationToken));
    }

    public override async Task<TEntity> CreateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow => await CreateAsync(uow, entity, dismissSendEvent, upsert: false, cancellationToken));
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
                    ? GetQuery(uow).FirstOrDefault(customCheckExistingPredicate)
                    : GetQuery(uow).FirstOrDefault(p => p.Id.Equals(entity.Id));

                if (existingEntity != null)
                {
                    entity.Id = existingEntity.Id;

                    if (entity is IRowVersionEntity rowVersionEntity &&
                        existingEntity is IRowVersionEntity existingRowVersionEntity)
                        rowVersionEntity.ConcurrencyUpdateToken = existingRowVersionEntity.ConcurrencyUpdateToken;

                    return UpdateAsync(uow, entity, dismissSendEvent, cancellationToken);
                }

                return CreateAsync(uow, entity, dismissSendEvent, upsert: true, cancellationToken);
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

                var existingEntityIds =
                    (await GetTable(uow)
                        .AsQueryable()
                        .Where(p => entityIds.Contains(p.Id))
                        .Select(p => p.Id)
                        .Distinct()
                        .ToListAsync(cancellationToken))
                    .ToHashSet();

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
                var entity = await GetTable(uow).Find(p => p.Id.Equals(entityId)).FirstOrDefaultAsync(cancellationToken);

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
                var entities = await GetDbContext(uow)
                    .GetAllAsync(
                        GetQuery(uow).Where(p => entityIds.Contains(p.Id)),
                        cancellationToken);

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

    public virtual async Task<List<TEntity>> UpdateWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            async uow =>
            {
                var entities = await GetTable(uow).AsQueryable().Where(predicate).ToListAsync(cancellationToken);

                entities.ForEach(updateAction);

                return await UpdateManyAsync(uow, entities, dismissSendEvent, cancellationToken);
            });
    }

    protected async Task<TEntity> CreateAsync(IUnitOfWork uow, TEntity entity, bool dismissSendEvent, bool upsert = false, CancellationToken cancellationToken = default)
    {
        await EnsureEntityValid(entity, cancellationToken);

        if (upsert == false)
            await GetTable(uow).InsertOneAsync(entity, null, cancellationToken);
        else
            await GetTable(uow)
                .ReplaceOneAsync(
                    p => p.Id.Equals(entity.Id),
                    entity,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);

        if (!dismissSendEvent)
            await Cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Created),
                cancellationToken);

        return entity;
    }

    protected async Task<TEntity> UpdateAsync(IUnitOfWork uow, TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await EnsureEntityValid(entity, cancellationToken);

        if (entity is IRowVersionEntity rowVersionEntity)
        {
            var currentInMemoryConcurrencyUpdateToken = rowVersionEntity.ConcurrencyUpdateToken;
            var newUpdateConcurrencyUpdateToken = Guid.NewGuid();

            rowVersionEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;

            var result = await GetTable(uow)
                .ReplaceOneAsync(
                    p => p.Id.Equals(entity.Id) &&
                         (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == Guid.Empty ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken),
                    entity,
                    new ReplaceOptions
                    {
                        IsUpsert = false
                    },
                    cancellationToken);

            if (result.MatchedCount <= 0)
            {
                if (await GetTable(uow).AsQueryable().AnyAsync(p => p.Id.Equals(entity.Id), cancellationToken))
                    throw new PlatformDomainRowVersionConflictException(
                        $"Update {typeof(TEntity).Name} with Id:{entity.Id} has conflicted version.");
                throw new PlatformDomainEntityNotFoundException<TEntity>(entity.Id.ToString());
            }
        }
        else
        {
            var result = await GetTable(uow)
                .ReplaceOneAsync(
                    p => p.Id.Equals(entity.Id),
                    entity,
                    new ReplaceOptions
                    {
                        IsUpsert = false
                    },
                    cancellationToken);

            if (result.MatchedCount <= 0)
                throw new PlatformDomainEntityNotFoundException<TEntity>(entity.Id.ToString());
        }

        if (!dismissSendEvent)
            await Cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Updated),
                cancellationToken);
        return entity;
    }

    protected async Task<List<TEntity>> CreateManyAsync(IUnitOfWork uow, List<TEntity> entities, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await EnsureEntitiesValid(entities, cancellationToken);

        if (entities.Any())
            await GetTable(uow).InsertManyAsync(entities, null, cancellationToken);

        if (!dismissSendEvent)
            await Cqrs.SendEvents(
                entities.Select(
                    entity => new PlatformCqrsEntityEvent<TEntity>(
                        entity,
                        PlatformCqrsEntityEventCrudAction.Created)),
                cancellationToken);

        return entities;
    }

    protected async Task<List<TEntity>> UpdateManyAsync(IUnitOfWork uow, List<TEntity> entities, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        await entities.ForEachAsync(entity => UpdateAsync(uow, entity, dismissSendEvent, cancellationToken));

        return entities;
    }

    protected async Task DeleteAsync(IUnitOfWork uow, TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        var result = await GetTable(uow).DeleteOneAsync(p => p.Id.Equals(entity.Id), null, cancellationToken);

        if (result.DeletedCount > 0 && !dismissSendEvent)
            await Cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, PlatformCqrsEntityEventCrudAction.Deleted),
                cancellationToken);
    }

    protected async Task<List<TEntity>> DeleteManyAsync(IUnitOfWork uow, List<TEntity> entities, bool dismissSendEvent, CancellationToken cancellationToken)
    {
        var ids = entities.Select(p => p.Id).ToList();
        await GetTable(uow).DeleteManyAsync(p => ids.Contains(p.Id), cancellationToken);

        if (!dismissSendEvent)
            await Cqrs.SendEvents(
                entities.Select(
                    entity => new PlatformCqrsEntityEvent<TEntity>(
                        entity,
                        PlatformCqrsEntityEventCrudAction.Deleted)),
                cancellationToken);

        return entities;
    }
}
