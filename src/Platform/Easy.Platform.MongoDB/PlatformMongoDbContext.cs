using System.Linq.Expressions;
using Easy.Platform.Application;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Migration;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB;

public abstract class PlatformMongoDbContext<TDbContext> : IPlatformDbContext<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>, IPlatformDbContext<TDbContext>
{
    public const string EnsureIndexesMigrationName = "EnsureIndexesAsync";
    public const string PlatformInboxBusMessageCollectionName = "InboxEventBusMessage";
    public const string PlatformOutboxBusMessageCollectionName = "OutboxEventBusMessage";
    public const string PlatformDataMigrationHistoryCollectionName = "MigrationHistory";

    protected readonly IPlatformApplicationSettingContext ApplicationSettingContext;
    protected readonly Lazy<Dictionary<Type, string>> EntityTypeToCollectionNameDictionary;
    protected readonly PlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration;
    protected readonly IPlatformApplicationRequestContextAccessor RequestContextAccessor;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;

    private readonly Lazy<ILogger> lazyLogger;

    private bool disposed;

    public PlatformMongoDbContext(
        IPlatformMongoDatabase<TDbContext> database,
        ILoggerFactory loggerFactory,
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationSettingContext applicationSettingContext)
    {
        Database = database.Value;

        RequestContextAccessor = requestContextAccessor;
        PersistenceConfiguration = persistenceConfiguration;
        RootServiceProvider = rootServiceProvider;
        ApplicationSettingContext = applicationSettingContext;
        EntityTypeToCollectionNameDictionary = new Lazy<Dictionary<Type, string>>(BuildEntityTypeToCollectionNameDictionary);

        lazyLogger = new Lazy<ILogger>(() => CreateLogger(loggerFactory));
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<PlatformInboxBusMessage> InboxBusMessageCollection =>
        Database.GetCollection<PlatformInboxBusMessage>(GetCollectionName<PlatformInboxBusMessage>());

    public IMongoCollection<PlatformOutboxBusMessage> OutboxBusMessageCollection =>
        Database.GetCollection<PlatformOutboxBusMessage>(GetCollectionName<PlatformOutboxBusMessage>());

    public IMongoCollection<PlatformDataMigrationHistory> DataMigrationHistoryCollection =>
        Database.GetCollection<PlatformDataMigrationHistory>(DataMigrationHistoryCollectionName);

    public virtual string DataMigrationHistoryCollectionName => "ApplicationDataMigrationHistory";

    public IMongoCollection<PlatformMongoMigrationHistory> MigrationHistoryCollection =>
        Database.GetCollection<PlatformMongoMigrationHistory>(MigrationHistoryCollectionName);

    public virtual string MigrationHistoryCollectionName => "MigrationHistory";

    public virtual int ExecutionManyPageSize => 100;

    public virtual string DbInitializedMigrationHistoryName => PlatformDataMigrationHistory.DefaultDbInitializedMigrationHistoryName;

    public async Task UpsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity, CancellationToken cancellationToken = default)
    {
        var existingEntity = await DataMigrationHistoryQuery().Where(p => p.Name == entity.Name).FirstOrDefaultAsync(cancellationToken);

        if (existingEntity == null)
            await DataMigrationHistoryCollection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        else
        {
            if (entity is IRowVersionEntity { ConcurrencyUpdateToken: null })
                entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = existingEntity.As<IRowVersionEntity>().ConcurrencyUpdateToken;

            var toBeUpdatedEntity = entity;

            var currentInMemoryConcurrencyUpdateToken = toBeUpdatedEntity.ConcurrencyUpdateToken;
            var newUpdateConcurrencyUpdateToken = Ulid.NewUlid().ToString();

            toBeUpdatedEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;

            var result = await DataMigrationHistoryCollection
                .ReplaceOneAsync(
                    p => p.Name == entity.Name &&
                         (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == "" ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken),
                    entity,
                    new ReplaceOptions { IsUpsert = false },
                    cancellationToken);

            if (result.MatchedCount <= 0)
            {
                if (await DataMigrationHistoryCollection.AsQueryable().AnyAsync(p => p.Name == entity.Name, cancellationToken))
                {
                    throw new PlatformDomainRowVersionConflictException(
                        $"Update {nameof(PlatformDataMigrationHistory)} with Name:{toBeUpdatedEntity.Name} has conflicted version.");
                }

                throw new PlatformDomainEntityNotFoundException<PlatformDataMigrationHistory>(toBeUpdatedEntity.Name);
            }
        }
    }

    public IQueryable<PlatformDataMigrationHistory> DataMigrationHistoryQuery()
    {
        return DataMigrationHistoryCollection.AsQueryable();
    }

    public async Task ExecuteWithNewDbContextInstanceAsync(Func<IPlatformDbContext, Task> fn)
    {
        await RootServiceProvider.ExecuteInjectScopedAsync(async (TDbContext context) => await fn(context));
    }

    public IPlatformUnitOfWork? MappedUnitOfWork { get; set; }
    public ILogger Logger => lazyLogger.Value;

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        // Store stack trace before call Migrate() to keep the original stack trace to log
        // after Migrate() will lose full stack trace (may because it connects async to other external service)
        var fullStackTrace = PlatformEnvironment.StackTrace();

        try
        {
            await Migrate();
            await InsertDbInitializedApplicationDataMigrationHistory();
            await SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.BeautifyStackTrace(), "PlatformMongoDbContext {Type} Initialize failed.", GetType().Name);

            throw new Exception(
                $"{GetType().Name} Initialize failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                ex);
        }

        async Task InsertDbInitializedApplicationDataMigrationHistory()
        {
            if (!await DataMigrationHistoryCollection.AsQueryable()
                .AnyAsync(p => p.Name == DbInitializedMigrationHistoryName))
            {
                await DataMigrationHistoryCollection.InsertOneAsync(
                    new PlatformDataMigrationHistory(DbInitializedMigrationHistoryName)
                    {
                        Status = PlatformDataMigrationHistory.Statuses.Processed
                    });
            }
        }
    }

    public Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.FirstAsync(cancellationToken);
    }

    public Task<int> CountAsync<TEntity>(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return GetCollection<TEntity>()
            .CountDocumentsAsync(predicate != null ? Builders<TEntity>.Filter.Where(predicate) : Builders<TEntity>.Filter.Empty, cancellationToken: cancellationToken)
            .Then(result => (int)result);
    }

    public Task<TResult> FirstOrDefaultAsync<TEntity, TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return queryBuilder(GetQuery<TEntity>()).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return source.CountAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<TEntity>(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return GetCollection<TEntity>()
            .Find(predicate != null ? Builders<TEntity>.Filter.Where(predicate) : Builders<TEntity>.Filter.Empty)
            .Limit(1)
            .CountDocumentsAsync(cancellationToken)
            .Then(result => result > 0);
    }

    public Task<bool> AnyAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return source.AnyAsync(cancellationToken);
    }

    public Task<List<T>> GetAllAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return source.ToListAsync(cancellationToken);
    }

    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return source.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<TResult>> GetAllAsync<TEntity, TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return queryBuilder(GetQuery<TEntity>()).ToListAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Not support real transaction tracking. No need to do anything
        return Task.FromResult(0);
    }

    public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IEntity
    {
        return GetCollection<TEntity>().AsQueryable();
    }

    public void RunCommand(string command)
    {
        Database.RunCommand<BsonDocument>(command);
    }

    public Task MigrateDataAsync(IServiceProvider serviceProvider)
    {
        return this.As<IPlatformDbContext>().MigrateDataAsync<TDbContext>(serviceProvider, RootServiceProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<List<TEntity>> CreateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.IsEmpty()) return entities;

        var toBeCreatedEntities = entities
            .SelectList(
                entity => entity.PipeIf(
                        entity.IsAuditedUserEntity(),
                        p => p.As<IUserAuditedEntity>()
                            .SetCreatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                            .As<TEntity>())
                    .WithIf(
                        entity is IRowVersionEntity { ConcurrencyUpdateToken: null },
                        entity => entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = Ulid.NewUlid().ToString()));

        var bulkCreateOps = toBeCreatedEntities
            .Select(toBeCreatedEntity => new InsertOneModel<TEntity>(toBeCreatedEntity))
            .ToList();

        await GetTable<TEntity>().BulkWriteAsync(bulkCreateOps, new BulkWriteOptions { IsOrdered = false }, cancellationToken);

        if (!dismissSendEvent && PlatformCqrsEntityEvent.IsAnyEntityEventHandlerRegisteredForEntity<TEntity>(RootServiceProvider))
        {
            await toBeCreatedEntities.ParallelAsync(
                toBeCreatedEntity => PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                    RootServiceProvider,
                    MappedUnitOfWork,
                    toBeCreatedEntity,
                    entity => Task.FromResult(entity),
                    false,
                    eventCustomConfig,
                    () => RequestContextAccessor.Current.GetAllKeyValues(),
                    PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, false),
                    cancellationToken));

            await SendBulkEntitiesEvent<TEntity, TPrimaryKey>(
                toBeCreatedEntities,
                PlatformCqrsEntityEventCrudAction.Created,
                eventCustomConfig,
                cancellationToken);
        }

        return toBeCreatedEntities;
    }

    public Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return UpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public async Task<TEntity> SetAsync<TEntity, TPrimaryKey>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await InternalUpdateOrSetAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent: true, null, onlySetData: true, cancellationToken);
    }

    public async Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.IsEmpty()) return entities;

        var toBeUpdatedItems = await entities.ParallelAsync(
            async entity =>
            {
                var isEntityRowVersionEntityMissingConcurrencyUpdateToken = entity is IRowVersionEntity { ConcurrencyUpdateToken: null };
                TEntity existingEntity = null;

                if (!dismissSendEvent &&
                    PlatformCqrsEntityEvent.IsAnyEntityEventHandlerRegisteredForEntity<TEntity>(RootServiceProvider) &&
                    entity.HasTrackValueUpdatedDomainEventAttribute())
                {
                    existingEntity = MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()) ??
                                     await GetQuery<TEntity>()
                                         .Where(BuildExistingEntityPredicate(entity))
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .EnsureFound($"Entity {typeof(TEntity).Name} with [Id:{entity.Id}] not found to update")
                                         .ThenActionIf(
                                             p => p != null,
                                             p => MappedUnitOfWork?.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p));

                    if (!existingEntity.Id.Equals(entity.Id)) entity.Id = existingEntity.Id;
                }

                if (isEntityRowVersionEntityMissingConcurrencyUpdateToken)
                {
                    entity.As<IRowVersionEntity>().ConcurrencyUpdateToken =
                        existingEntity?.As<IRowVersionEntity>().ConcurrencyUpdateToken ??
                        await GetQuery<TEntity>()
                            .Where(BuildExistingEntityPredicate(entity))
                            .Select(p => ((IRowVersionEntity)p).ConcurrencyUpdateToken)
                            .FirstOrDefaultAsync(cancellationToken);
                }

                var toBeUpdatedEntity = entity
                    .PipeIf(
                        entity is IDateAuditedEntity,
                        p => p.As<IDateAuditedEntity>().With(auditedEntity => auditedEntity.LastUpdatedDate = DateTime.UtcNow).As<TEntity>())
                    .PipeIf(
                        entity.IsAuditedUserEntity(),
                        p => p.As<IUserAuditedEntity>()
                            .SetLastUpdatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                            .As<TEntity>());
                string? currentInMemoryConcurrencyUpdateToken = null;

                if (toBeUpdatedEntity is IRowVersionEntity toBeUpdatedRowVersionEntity)
                {
                    currentInMemoryConcurrencyUpdateToken = toBeUpdatedRowVersionEntity.ConcurrencyUpdateToken;
                    var newUpdateConcurrencyUpdateToken = Ulid.NewUlid().ToString();

                    toBeUpdatedRowVersionEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;
                }

                var bulkUpdateOp = new ReplaceOneModel<TEntity>(
                    Builders<TEntity>.Filter.Pipe(
                        filterBuilder => currentInMemoryConcurrencyUpdateToken != null
                            ? filterBuilder.Where(
                                p => p.Id.Equals(toBeUpdatedEntity.Id) &&
                                     (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                                      ((IRowVersionEntity)p).ConcurrencyUpdateToken == "" ||
                                      ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken))
                            : filterBuilder.Where(p => p.Id.Equals(toBeUpdatedEntity.Id))),
                    toBeUpdatedEntity)
                {
                    IsUpsert = false
                };

                return (toBeUpdatedEntity, bulkUpdateOp, existingEntity, currentInMemoryConcurrencyUpdateToken);
            });

        await GetTable<TEntity>()
            .BulkWriteAsync(toBeUpdatedItems.SelectList(p => p.bulkUpdateOp), new BulkWriteOptions { IsOrdered = false }, cancellationToken)
            .ThenActionAsync(
                async result =>
                {
                    if (result.MatchedCount != toBeUpdatedItems.Count)
                    {
                        var toBeUpdatedEntityIds = toBeUpdatedItems.Select(p => p.toBeUpdatedEntity.Id).ToHashSet();

                        if (toBeUpdatedItems.First().toBeUpdatedEntity is IRowVersionEntity)
                        {
                            var existingEntityIdToConcurrencyUpdateTokenDict = await GetQuery<TEntity>()
                                .Where(p => toBeUpdatedEntityIds.Contains(p.Id))
                                .Select(p => new { p.Id, ((IRowVersionEntity)p).ConcurrencyUpdateToken })
                                .ToListAsync(cancellationToken)
                                .Then(items => items.ToDictionary(p => p.Id, p => p.ConcurrencyUpdateToken));

                            toBeUpdatedItems
                                .ForEach(
                                    p =>
                                    {
                                        if (!existingEntityIdToConcurrencyUpdateTokenDict.TryGetValue(
                                            p.toBeUpdatedEntity.Id,
                                            out var existingEntityConcurrencyToken))
                                            throw new PlatformDomainEntityNotFoundException<TEntity>(p.toBeUpdatedEntity.Id.ToString());
                                        if (existingEntityConcurrencyToken != p.currentInMemoryConcurrencyUpdateToken)
                                        {
                                            throw new PlatformDomainRowVersionConflictException(
                                                $"Update {typeof(TEntity).Name} with Id:{p.toBeUpdatedEntity.Id} has conflicted version.");
                                        }
                                    });
                        }

                        var existingEntityIds = await GetQuery<TEntity>()
                            .Where(p => toBeUpdatedEntityIds.Contains(p.Id))
                            .Select(p => p.Id)
                            .ToListAsync(cancellationToken)
                            .Then(p => p.ToHashSet());

                        toBeUpdatedEntityIds
                            .ForEach(
                                toBeUpdatedEntityId =>
                                {
                                    if (!existingEntityIds.Contains(toBeUpdatedEntityId))
                                        throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntityId.ToString());
                                });
                    }
                });

        if (!dismissSendEvent && PlatformCqrsEntityEvent.IsAnyEntityEventHandlerRegisteredForEntity<TEntity>(RootServiceProvider))
        {
            await toBeUpdatedItems.ParallelAsync(
                toBeUpdatedItem => PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                    RootServiceProvider,
                    MappedUnitOfWork,
                    toBeUpdatedItem.toBeUpdatedEntity,
                    toBeUpdatedItem.existingEntity ??
                    MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(toBeUpdatedItem.toBeUpdatedEntity.Id.ToString()),
                    entity => Task.FromResult((entity, true)),
                    false,
                    eventCustomConfig,
                    () => RequestContextAccessor.Current.GetAllKeyValues(),
                    PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, false),
                    cancellationToken));
            await SendBulkEntitiesEvent<TEntity, TPrimaryKey>(
                toBeUpdatedItems.SelectList(p => p.toBeUpdatedEntity),
                PlatformCqrsEntityEventCrudAction.Updated,
                eventCustomConfig,
                cancellationToken);
        }

        return toBeUpdatedItems.SelectList(
            p =>
            {
                MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(p.toBeUpdatedEntity.Id.ToString());
                return p.toBeUpdatedEntity;
            });

        Expression<Func<TEntity, bool>> BuildExistingEntityPredicate(TEntity entity)
        {
            return entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                ? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!
                : p => p.Id.Equals(entity.Id);
        }
    }

    public async Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entity = GetQuery<TEntity>().FirstOrDefault(p => p.Id.Equals(entityId));

        if (entity != null) await DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken);

        return entity;
    }

    public async Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await PlatformCqrsEntityEvent.ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                entity,
                async entity =>
                {
                    await GetTable<TEntity>().DeleteOneAsync(p => p.Id.Equals(entity.Id), null, cancellationToken);

                    return entity;
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken)
            .ThenAction(
                entity =>
                {
                    MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(entity.Id.ToString());
                });
    }

    public async Task<List<TPrimaryKey>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
            return await DeleteManyAsync<TEntity, TPrimaryKey>(p => entityIds.Contains(p.Id), true, eventCustomConfig, cancellationToken).Then(() => entityIds);

        var entities = await GetAllAsync(GetQuery<TEntity>().Where(p => entityIds.Contains(p.Id)), cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, false, eventCustomConfig, cancellationToken).Then(() => entityIds);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.Count == 0) return entities;

        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
        {
            var deleteEntitiesPredicate = entities.FirstOrDefault()?.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                ? entities
                    .Select(
                        entity => entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr())
                    .Aggregate((currentExpr, nextExpr) => currentExpr.Or(nextExpr))
                : p => entities.Select(e => e.Id).Contains(p.Id);

            return await DeleteManyAsync<TEntity, TPrimaryKey>(
                    deleteEntitiesPredicate,
                    true,
                    eventCustomConfig,
                    cancellationToken)
                .Then(
                    _ =>
                    {
                        entities.ForEach(p => MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(p.Id.ToString()));
                        return entities;
                    });
        }

        return await entities
            .ParallelAsync(entity => DeleteAsync<TEntity, TPrimaryKey>(entity, false, eventCustomConfig, cancellationToken))
            .ThenActionAsync(
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Deleted, eventCustomConfig, cancellationToken))
            .Then(
                entities =>
                {
                    entities.ForEach(p => MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(p.Id.ToString()));
                    return entities;
                });
    }

    public async Task<int> DeleteManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
            return (int)await GetTable<TEntity>().DeleteManyAsync(predicate, null, cancellationToken).Then(p => p.DeletedCount);

        var entities = await GetAllAsync(GetQuery<TEntity>().Where(predicate), cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, false, eventCustomConfig, cancellationToken).Then(_ => entities.Count);
    }

    public async Task<int> DeleteManyAsync<TEntity, TPrimaryKey>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var totalCount = await CountAsync(queryBuilder(GetQuery<TEntity>()), cancellationToken);

        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
        {
            var ids = await GetAllAsync<TEntity, TPrimaryKey>(
                query => queryBuilder(query).Select(p => p.Id),
                cancellationToken);

            await GetTable<TEntity>().DeleteManyAsync(p => ids.Contains(p.Id), null, cancellationToken).Then(p => p.DeletedCount);
        }
        else
        {
            await Util.Pager.ExecuteScrollingPagingAsync(
                async () =>
                {
                    var entities = await GetAllAsync(queryBuilder(GetQuery<TEntity>()).Take(ExecutionManyPageSize), cancellationToken);

                    await DeleteManyAsync<TEntity, TPrimaryKey>(entities, false, eventCustomConfig, cancellationToken).Then(_ => entities.Count);

                    return entities;
                },
                maxExecutionCount: totalCount / ExecutionManyPageSize,
                cancellationToken: cancellationToken);
        }

        return totalCount;
    }

    public Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, false, eventCustomConfig, cancellationToken);
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, null, customCheckExistingPredicate, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity? existingEntity,
        Expression<Func<TEntity, bool>>? customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var existingEntityPredicate = customCheckExistingPredicate != null ||
                                      entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
            ? customCheckExistingPredicate ?? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!
            : p => p.Id.Equals(entity.Id);

        existingEntity ??= MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()) ??
                           await GetQuery<TEntity>()
                               .Where(existingEntityPredicate)
                               .FirstOrDefaultAsync(cancellationToken)
                               .ThenActionIf(
                                   p => p != null,
                                   p => MappedUnitOfWork?.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p));

        if (existingEntity != null)
        {
            return await UpdateAsync<TEntity, TPrimaryKey>(
                entity.WithIf(!entity.Id.Equals(existingEntity.Id), entity => entity.Id = existingEntity.Id),
                existingEntity,
                dismissSendEvent,
                eventCustomConfig,
                cancellationToken);
        }

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, true, eventCustomConfig, cancellationToken);
    }

    public async Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.Any())
        {
            var entityIds = entities.Select(p => p.Id);

            var existingEntitiesQuery = GetQuery<TEntity>()
                .Pipe(
                    query => customCheckExistingPredicateBuilder != null ||
                             entities.FirstOrDefault()?.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                        ? query.Where(
                            entities
                                .Select(
                                    entity => customCheckExistingPredicateBuilder?.Invoke(entity) ??
                                              entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr())
                                .Aggregate((currentExpr, nextExpr) => currentExpr.Or(nextExpr)))
                        : query.Where(p => entityIds.Contains(p.Id)));

            // Only need to check by entityIds if no custom check condition
            if (customCheckExistingPredicateBuilder == null &&
                entities.FirstOrDefault()?.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() == null)
            {
                var existingEntityIds = await existingEntitiesQuery.ToListAsync(cancellationToken)
                    .Then(
                        items => items
                            .PipeAction(items => items.ForEach(p => MappedUnitOfWork?.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p)))
                            .Pipe(existingEntities => existingEntities.Select(p => p.Id).ToHashSet()));
                var (toUpdateEntities, newEntities) = entities.WhereSplitResult(p => existingEntityIds.Contains(p.Id));

                await Util.TaskRunner.WhenAll(
                    CreateManyAsync<TEntity, TPrimaryKey>(
                        newEntities,
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken),
                    UpdateManyAsync<TEntity, TPrimaryKey>(
                        toUpdateEntities,
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken));
            }
            else
            {
                var existingEntities = await existingEntitiesQuery.ToListAsync(cancellationToken)
                    .Then(
                        items => items
                            .PipeAction(items => items.ForEach(p => MappedUnitOfWork?.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p))));

                var toUpsertEntityToExistingEntityPairs = entities.SelectList(
                    toUpsertEntity =>
                    {
                        var matchedExistingEntity = existingEntities.FirstOrDefault(
                            existingEntity => customCheckExistingPredicateBuilder?.Invoke(toUpsertEntity).Compile()(existingEntity) ??
                                              toUpsertEntity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr().Compile()(existingEntity));

                        // Update to correct the id of toUpdateEntity to the matched existing entity Id
                        if (matchedExistingEntity != null) toUpsertEntity.Id = matchedExistingEntity.Id;

                        return new { toUpsertEntity, matchedExistingEntity };
                    });

                var (existingToUpdateEntities, newEntities) = toUpsertEntityToExistingEntityPairs.WhereSplitResult(p => p.matchedExistingEntity != null);

                await Util.TaskRunner.WhenAll(
                    CreateManyAsync<TEntity, TPrimaryKey>(
                        newEntities.Select(p => p.toUpsertEntity).ToList(),
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken),
                    UpdateManyAsync<TEntity, TPrimaryKey>(
                        existingToUpdateEntities.Select(p => p.toUpsertEntity).ToList(),
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken));
            }
        }

        return entities;
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformMongoDbContext<>).GetNameOrGenericTypeName() + $"-{GetType().Name}");
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity? existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await InternalUpdateOrSetAsync<TEntity, TPrimaryKey>(entity, existingEntity, dismissSendEvent, eventCustomConfig, onlySetData: false, cancellationToken);
    }

    private async Task<TEntity> InternalUpdateOrSetAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        bool onlySetData,
        CancellationToken cancellationToken) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var isEntityRowVersionEntityMissingConcurrencyUpdateToken = entity is IRowVersionEntity { ConcurrencyUpdateToken: null };

        if (existingEntity == null &&
            !onlySetData &&
            !dismissSendEvent &&
            PlatformCqrsEntityEvent.IsAnyEntityEventHandlerRegisteredForEntity<TEntity>(RootServiceProvider) &&
            entity.HasTrackValueUpdatedDomainEventAttribute())
        {
            existingEntity = MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()) ??
                             await GetQuery<TEntity>()
                                 .Where(BuildExistingEntityPredicate())
                                 .FirstOrDefaultAsync(cancellationToken)
                                 .EnsureFound($"Entity {typeof(TEntity).Name} with [Id:{entity.Id}] not found to update")
                                 .ThenActionIf(
                                     p => p != null,
                                     p => MappedUnitOfWork?.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p));

            if (!existingEntity.Id.Equals(entity.Id)) entity.Id = existingEntity.Id;
        }

        if (isEntityRowVersionEntityMissingConcurrencyUpdateToken && !onlySetData)
        {
            entity.As<IRowVersionEntity>().ConcurrencyUpdateToken =
                existingEntity?.As<IRowVersionEntity>().ConcurrencyUpdateToken ??
                await GetQuery<TEntity>()
                    .Where(BuildExistingEntityPredicate())
                    .Select(p => ((IRowVersionEntity)p).ConcurrencyUpdateToken)
                    .FirstOrDefaultAsync<string>(cancellationToken);
        }

        if (existingEntity != null && !ReferenceEquals(entity, existingEntity) && entity.IsValuesEqual(existingEntity))
            return entity;

        var toBeUpdatedEntity = entity
            .PipeIf(
                entity is IDateAuditedEntity && !onlySetData,
                p => p.As<IDateAuditedEntity>().With(auditedEntity => auditedEntity.LastUpdatedDate = DateTime.UtcNow).As<TEntity>())
            .PipeIf(
                entity.IsAuditedUserEntity() && !onlySetData,
                p => p.As<IUserAuditedEntity>()
                    .SetLastUpdatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                    .As<TEntity>());

        if (toBeUpdatedEntity is IRowVersionEntity toBeUpdatedRowVersionEntity && !onlySetData)
        {
            var currentInMemoryConcurrencyUpdateToken = toBeUpdatedRowVersionEntity.ConcurrencyUpdateToken;
            var newUpdateConcurrencyUpdateToken = Ulid.NewUlid().ToString();

            toBeUpdatedRowVersionEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;

            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, ReplaceOneResult>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeUpdatedEntity,
                existingEntity ?? MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()),
                entity => GetTable<TEntity>()
                    .ReplaceOneAsync(
                        p => p.Id.Equals(entity.Id) &&
                             (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                              ((IRowVersionEntity)p).ConcurrencyUpdateToken == "" ||
                              ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken),
                        entity,
                        new ReplaceOptions { IsUpsert = false },
                        cancellationToken)
                    .Then(p => (p, true)),
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            if (result.MatchedCount <= 0)
            {
                if (await GetTable<TEntity>().AsQueryable().AnyAsync(p => p.Id.Equals(toBeUpdatedEntity.Id), cancellationToken))
                {
                    MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(toBeUpdatedEntity.Id.ToString());

                    throw new PlatformDomainRowVersionConflictException(
                        $"Update {typeof(TEntity).Name} with Id:{toBeUpdatedEntity.Id} has conflicted version.");
                }

                throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntity.Id.ToString());
            }
        }
        else
        {
            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, ReplaceOneResult>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeUpdatedEntity,
                existingEntity ?? MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()),
                _ => GetTable<TEntity>()
                    .ReplaceOneAsync(
                        p => p.Id.Equals(toBeUpdatedEntity.Id),
                        toBeUpdatedEntity,
                        new ReplaceOptions { IsUpsert = false },
                        cancellationToken)
                    .Then(p => (p, true)),
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            if (result.MatchedCount <= 0)
                throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntity.Id.ToString());
        }

        MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(entity.Id.ToString());

        return entity;

        Expression<Func<TEntity, bool>> BuildExistingEntityPredicate()
        {
            return entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                ? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!
                : p => p.Id.Equals(entity.Id);
        }
    }

    public virtual async Task EnsureIndexesAsync(bool recreate = false)
    {
        if (!recreate && await IsEnsureIndexesMigrationExecuted()) return;

        Logger.LogInformation("[{TargetName}] EnsureIndexesAsync STARTED.", GetType().Name);

        await Task.WhenAll(
            EnsureMigrationHistoryCollectionIndexesAsync(recreate),
            EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(recreate),
            EnsureInboxBusMessageCollectionIndexesAsync(recreate),
            EnsureOutboxBusMessageCollectionIndexesAsync(recreate),
            InternalEnsureIndexesAsync(recreate));

        if (!await IsEnsureIndexesMigrationExecuted())
        {
            await MigrationHistoryCollection.InsertOneAsync(
                new PlatformMongoMigrationHistory(EnsureIndexesMigrationName));
        }

        Logger.LogInformation("[{TargetName}] EnsureIndexesAsync FINISHED.", GetType().Name);
    }

    public string GenerateId()
    {
        return new BsonObjectId(ObjectId.GenerateNewId()).ToString();
    }

    public async Task Migrate()
    {
        await EnsureIndexesAsync();

        EnsureAllMigrationExecutorsHasUniqueName();

        var dbInitializedDate =
            DataMigrationHistoryQuery().FirstOrDefault(p => p.Name == DbInitializedMigrationHistoryName)?.CreatedDate ??
            DateTime.UtcNow;

        await NotExecutedMigrationExecutors()
            .ForEachAsync(
                async migrationExecutor =>
                {
                    if (migrationExecutor.OnlyForDbInitBeforeDate == null ||
                        dbInitializedDate < migrationExecutor.OnlyForDbInitBeforeDate)
                    {
                        Logger.LogInformation("Migration {MigrationExecutorName} STARTED.", migrationExecutor.Name);

                        await migrationExecutor.Execute((TDbContext)this);
                        await MigrationHistoryCollection.InsertOneAsync(new PlatformMongoMigrationHistory(migrationExecutor.Name));
                        await SaveChangesAsync();

                        Logger.LogInformation("Migration {MigrationExecutorName} FINISHED.", migrationExecutor.Name);
                    }
                });
    }

    public string GetCollectionName<TEntity>()
    {
        if (TryGetCollectionName<TEntity>(out var collectionName))
            return collectionName;

        if (GetPlatformEntityCollectionName<TEntity>() != null)
            return GetPlatformEntityCollectionName<TEntity>();

        throw new Exception(
            $"Missing collection name mapping item for entity {typeof(TEntity).Name}. Please define it in return of {nameof(EntityTypeToCollectionNameMaps)} method.");
    }

    public virtual IMongoCollection<TEntity> GetCollection<TEntity>()
    {
        return Database.GetCollection<TEntity>(GetCollectionName<TEntity>());
    }

    public IMongoCollection<TEntity> GetTable<TEntity>() where TEntity : class, IEntity, new()
    {
        return GetCollection<TEntity>();
    }

    public virtual async Task EnsureMigrationHistoryCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !await IsEnsureIndexesMigrationExecuted())
            await MigrationHistoryCollection.Indexes.DropAllAsync();

        if (recreate || !await IsEnsureIndexesMigrationExecuted())
        {
            await MigrationHistoryCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformMongoMigrationHistory>(
                    Builders<PlatformMongoMigrationHistory>.IndexKeys.Ascending(p => p.Name),
                    new CreateIndexOptions
                    {
                        Unique = true
                    })
            ]);
        }
    }

    public virtual async Task EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !await IsEnsureIndexesMigrationExecuted())
            await DataMigrationHistoryCollection.Indexes.DropAllAsync();

        if (recreate || !await IsEnsureIndexesMigrationExecuted())
        {
            await DataMigrationHistoryCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformDataMigrationHistory>(
                    Builders<PlatformDataMigrationHistory>.IndexKeys.Ascending(p => p.Name),
                    new CreateIndexOptions
                    {
                        Unique = true
                    }),
                new CreateIndexModel<PlatformDataMigrationHistory>(
                    Builders<PlatformDataMigrationHistory>.IndexKeys.Ascending(p => p.Status))
            ]);
        }
    }

    public virtual async Task EnsureInboxBusMessageCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !await IsEnsureIndexesMigrationExecuted())
            await InboxBusMessageCollection.Indexes.DropAllAsync();

        if (recreate || !await IsEnsureIndexesMigrationExecuted())
        {
            await InboxBusMessageCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformInboxBusMessage>(
                    Builders<PlatformInboxBusMessage>.IndexKeys
                        .Ascending(p => p.ConsumeStatus)
                        .Ascending(p => p.NextRetryProcessAfter)
                        .Ascending(p => p.ForApplicationName)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformInboxBusMessage>(
                    Builders<PlatformInboxBusMessage>.IndexKeys
                        .Ascending(p => p.ConsumeStatus)
                        .Ascending(p => p.LastProcessingPingDate)
                        .Ascending(p => p.ForApplicationName)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformInboxBusMessage>(
                    Builders<PlatformInboxBusMessage>.IndexKeys
                        .Ascending(p => p.ConsumeStatus)
                        .Ascending(p => p.CreatedDate))
            ]);
        }
    }

    public virtual async Task EnsureOutboxBusMessageCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !await IsEnsureIndexesMigrationExecuted())
            await OutboxBusMessageCollection.Indexes.DropAllAsync();

        if (recreate || !await IsEnsureIndexesMigrationExecuted())
        {
            await OutboxBusMessageCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformOutboxBusMessage>(
                    Builders<PlatformOutboxBusMessage>.IndexKeys
                        .Ascending(p => p.SendStatus)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformOutboxBusMessage>(
                    Builders<PlatformOutboxBusMessage>.IndexKeys
                        .Ascending(p => p.SendStatus)
                        .Ascending(p => p.NextRetryProcessAfter)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformOutboxBusMessage>(
                    Builders<PlatformOutboxBusMessage>.IndexKeys
                        .Ascending(p => p.SendStatus)
                        .Ascending(p => p.LastProcessingPingDate)
                        .Ascending(p => p.CreatedDate))
            ]);
        }
    }

    public abstract Task InternalEnsureIndexesAsync(bool recreate = false);

    /// <summary>
    /// This is used for <see cref="TryGetCollectionName{TEntity}" /> to return the collection name for TEntity
    /// </summary>
    public virtual List<KeyValuePair<Type, string>> EntityTypeToCollectionNameMaps() { return null; }

    /// <summary>
    /// TryGetCollectionName for <see cref="GetCollectionName{TEntity}" /> to return the entity collection.
    /// Default will get from return of <see cref="EntityTypeToCollectionNameMaps" />
    /// </summary>
    protected virtual bool TryGetCollectionName<TEntity>(out string collectionName)
    {
        if (EntityTypeToCollectionNameDictionary.Value == null ||
            !EntityTypeToCollectionNameDictionary.Value.ContainsKey(typeof(TEntity)))
        {
            collectionName = GetPlatformEntityCollectionName<TEntity>() ?? typeof(TEntity).Name;
            return true;
        }

        return EntityTypeToCollectionNameDictionary.Value.TryGetValue(typeof(TEntity), out collectionName);
    }

    protected async Task<bool> IsEnsureIndexesMigrationExecuted()
    {
        return await MigrationHistoryCollection.AsQueryable().AnyAsync(p => p.Name == EnsureIndexesMigrationName);
    }

    protected async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        bool upsert = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var toBeCreatedEntity = entity
            .PipeIf(
                entity.IsAuditedUserEntity(),
                p => p.As<IUserAuditedEntity>()
                    .SetCreatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                    .As<TEntity>())
            .WithIf(
                entity is IRowVersionEntity { ConcurrencyUpdateToken: null },
                entity => entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = Ulid.NewUlid().ToString());

        if (upsert == false)
        {
            await PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeCreatedEntity,
                entity => GetTable<TEntity>().InsertOneAsync(entity, null, cancellationToken).Then(() => entity),
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);
        }
        else
        {
            await PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeCreatedEntity,
                entity => GetTable<TEntity>()
                    .ReplaceOneAsync(
                        p => p.Id.Equals(entity.Id),
                        entity,
                        new ReplaceOptions { IsUpsert = true },
                        cancellationToken)
                    .Then(() => entity),
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);
        }

        return toBeCreatedEntity;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Release managed resources
            }

            // Release unmanaged resources

            disposed = true;
        }
    }

    ~PlatformMongoDbContext()
    {
        Dispose(false);
    }

    protected List<PlatformMongoMigrationExecutor<TDbContext>> ScanAllMigrationExecutors()
    {
        var results = GetType()
            .Assembly.GetTypes()
            .Where(p => p.IsAssignableTo(typeof(PlatformMongoMigrationExecutor<TDbContext>)) && !p.IsAbstract)
            .Select(p => (PlatformMongoMigrationExecutor<TDbContext>)Activator.CreateInstance(p))
            .Where(p => p != null)
            .ToList();
        return results;
    }

    protected void EnsureAllMigrationExecutorsHasUniqueName()
    {
        var duplicatedMigrationNames = ScanAllMigrationExecutors()
            .GroupBy(p => p.Name)
            .ToDictionary(p => p.Key, p => p.Count())
            .Where(p => p.Value > 1)
            .ToList();

        if (duplicatedMigrationNames.Any())
            throw new Exception($"Mongo Migration Executor Names is duplicated. Duplicated name: {duplicatedMigrationNames.First()}");
    }

    protected List<PlatformMongoMigrationExecutor<TDbContext>> NotExecutedMigrationExecutors()
    {
        var executedMigrationNames = MigrationHistoryCollection.AsQueryable().Select(p => p.Name).ToHashSet();

        return ScanAllMigrationExecutors()
            .Where(p => !p.IsExpired())
            .OrderBy(x => x.GetOrderByValue())
            .ToList()
            .FindAll(me => !executedMigrationNames.Contains(me.Name));
    }

    protected Dictionary<Type, string> BuildEntityTypeToCollectionNameDictionary()
    {
        var entityTypeToCollectionNameMaps = EntityTypeToCollectionNameMaps();
        return entityTypeToCollectionNameMaps != null ? new Dictionary<Type, string>(entityTypeToCollectionNameMaps) : null;
    }

    protected static string GetPlatformEntityCollectionName<TEntity>()
    {
        if (typeof(TEntity).IsAssignableTo(typeof(PlatformInboxBusMessage)))
            return PlatformInboxBusMessageCollectionName;

        if (typeof(TEntity).IsAssignableTo(typeof(PlatformOutboxBusMessage)))
            return PlatformOutboxBusMessageCollectionName;

        if (typeof(TEntity).IsAssignableTo(typeof(PlatformMongoMigrationHistory)))
            return PlatformDataMigrationHistoryCollectionName;

        return null;
    }

    protected async Task SendBulkEntitiesEvent<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.IsEmpty()) return;

        await PlatformCqrsEntityEvent.SendBulkEntitiesEvent<TEntity, TPrimaryKey>(
            RootServiceProvider,
            MappedUnitOfWork,
            entities,
            crudAction,
            eventCustomConfig,
            () => RequestContextAccessor.Current.GetAllKeyValues(),
            PlatformCqrsEntityEvent.GetBulkEntitiesEventStackTrace<TEntity, TPrimaryKey>(RootServiceProvider),
            cancellationToken);
    }
}
