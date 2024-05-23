using System.Linq.Expressions;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Extensions;
using Easy.Platform.MongoDB.Migration;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Easy.Platform.MongoDB;

public abstract class PlatformMongoDbContext<TDbContext> : IPlatformDbContext<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>, IPlatformDbContext<TDbContext>
{
    public const string EnsureIndexesMigrationName = "EnsureIndexesAsync";
    public const string PlatformInboxBusMessageCollectionName = "InboxEventBusMessage";
    public const string PlatformOutboxBusMessageCollectionName = "OutboxEventBusMessage";
    public const string PlatformDataMigrationHistoryCollectionName = "MigrationHistory";

    protected readonly Lazy<Dictionary<Type, string>> EntityTypeToCollectionNameDictionary;
    protected readonly PlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;
    protected readonly IPlatformApplicationRequestContextAccessor UserContextAccessor;

    private readonly Lazy<ILogger> lazyLogger;

    private bool disposed;

    public PlatformMongoDbContext(
        IPlatformMongoDatabase<TDbContext> database,
        ILoggerFactory loggerFactory,
        IPlatformApplicationRequestContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        Database = database.Value;

        UserContextAccessor = userContextAccessor;
        PersistenceConfiguration = persistenceConfiguration;
        RootServiceProvider = rootServiceProvider;
        lazyLogger = new Lazy<ILogger>(() => CreateLogger(loggerFactory));

        EntityTypeToCollectionNameDictionary = new Lazy<Dictionary<Type, string>>(BuildEntityTypeToCollectionNameDictionary);
    }

    public IMongoDatabase Database { get; }

    /// <summary>
    /// If true enable show query to Debug output
    /// </summary>
    public virtual bool EnableDebugQueryLog { get; set; } = true;

    public IMongoCollection<PlatformInboxBusMessage> InboxBusMessageCollection =>
        Database.GetCollection<PlatformInboxBusMessage>(GetCollectionName<PlatformInboxBusMessage>());

    public IMongoCollection<PlatformOutboxBusMessage> OutboxBusMessageCollection =>
        Database.GetCollection<PlatformOutboxBusMessage>(GetCollectionName<PlatformOutboxBusMessage>());

    public IMongoCollection<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryCollection =>
        Database.GetCollection<PlatformDataMigrationHistory>(ApplicationDataMigrationHistoryCollectionName);

    public virtual string ApplicationDataMigrationHistoryCollectionName => "ApplicationDataMigrationHistory";

    public IMongoCollection<PlatformMongoMigrationHistory> MigrationHistoryCollection =>
        Database.GetCollection<PlatformMongoMigrationHistory>(DataMigrationHistoryCollectionName);

    public virtual string DataMigrationHistoryCollectionName => "MigrationHistory";

    public IQueryable<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryQuery => ApplicationDataMigrationHistoryCollection.AsQueryable();

    public async Task UpsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity)
    {
        var existingEntity = await ApplicationDataMigrationHistoryQuery.Where(p => p.Name == entity.Name).FirstOrDefaultAsync();

        if (existingEntity == null)
        {
            await ApplicationDataMigrationHistoryCollection.InsertOneAsync(entity);
        }
        else
        {
            if (entity is IRowVersionEntity { ConcurrencyUpdateToken: null })
                entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = existingEntity.As<IRowVersionEntity>().ConcurrencyUpdateToken;

            var toBeUpdatedEntity = entity;

            var currentInMemoryConcurrencyUpdateToken = toBeUpdatedEntity.ConcurrencyUpdateToken;
            var newUpdateConcurrencyUpdateToken = Guid.NewGuid();

            toBeUpdatedEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;

            var result = await ApplicationDataMigrationHistoryCollection
                .ReplaceOneAsync(
                    p => p.Name == entity.Name &&
                         (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == Guid.Empty ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken),
                    entity,
                    new ReplaceOptions { IsUpsert = false });

            if (result.MatchedCount <= 0)
            {
                if (await ApplicationDataMigrationHistoryCollection.AsQueryable().AnyAsync(p => p.Name == entity.Name))
                    throw new PlatformDomainRowVersionConflictException(
                        $"Update {nameof(PlatformDataMigrationHistory)} with Name:{toBeUpdatedEntity.Name} has conflicted version.");
                throw new PlatformDomainEntityNotFoundException<PlatformDataMigrationHistory>(toBeUpdatedEntity.Name);
            }
        }
    }

    public IQueryable<PlatformDataMigrationHistory> DataMigrationHistoryQuery()
    {
        return ApplicationDataMigrationHistoryQuery;
    }

    public async Task ExecuteWithNewDbContextInstanceAsync(Func<IPlatformDbContext, Task> fn)
    {
        await RootServiceProvider.ExecuteInjectScopedAsync(async (TDbContext context) => await fn(context));
    }

    public IPlatformUnitOfWork MappedUnitOfWork { get; set; }
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
            if (!await ApplicationDataMigrationHistoryCollection.AsQueryable()
                .AnyAsync(p => p.Name == PlatformDataMigrationHistory.DbInitializedMigrationHistoryName))
                await ApplicationDataMigrationHistoryCollection.InsertOneAsync(
                    new PlatformDataMigrationHistory(PlatformDataMigrationHistory.DbInitializedMigrationHistoryName));
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Not support real transaction tracking. No need to do anything
    }

    public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IEntity
    {
        return GetCollection<TEntity>().AsQueryable();
    }

    public void RunCommand(string command)
    {
        Database.RunCommand<BsonDocument>(command);
    }

    public Task MigrateApplicationDataAsync(IServiceProvider serviceProvider)
    {
        return this.As<IPlatformDbContext>().MigrateApplicationDataAsync<TDbContext>(serviceProvider, RootServiceProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Task<List<TEntity>> CreateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .SelectAsync(entity => CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken)),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize,
                cancellationToken: cancellationToken)
            .Then(result => result.SelectMany(p => p).ToList())
            .ThenActionIfAsync(
                !dismissSendEvent,
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Created, eventCustomConfig, cancellationToken));
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

    public Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .SelectAsync(entity => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken)),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize,
                cancellationToken: cancellationToken)
            .Then(result => result.SelectMany(p => p).ToList())
            .ThenActionIfAsync(
                !dismissSendEvent,
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Updated, eventCustomConfig, cancellationToken));
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
            eventCustomConfig: eventCustomConfig,
            requestContext: () => UserContextAccessor.Current.GetAllKeyValues(),
            stackTrace: RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTrace(),
            cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entities = await GetQuery<TEntity>().Where(p => entityIds.Contains(p.Id)).ToListAsync(cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await entities.SelectAsync(entity => DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken))
            .ThenActionIfAsync(
                !dismissSendEvent,
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Deleted, eventCustomConfig, cancellationToken));
    }

    public Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, upsert: false, eventCustomConfig, cancellationToken);
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var existingEntity = await GetQuery<TEntity>()
            .Pipe(
                query => customCheckExistingPredicate != null || entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                    ? query.Where(customCheckExistingPredicate ?? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!)
                    : query.Where(p => p.Id.Equals(entity.Id)))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEntity != null)
            return await UpdateAsync<TEntity, TPrimaryKey>(
                entity.WithIf(!entity.Id.Equals(existingEntity.Id), entity => entity.Id = existingEntity.Id),
                existingEntity,
                dismissSendEvent,
                eventCustomConfig,
                cancellationToken);

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, upsert: true, eventCustomConfig, cancellationToken);
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
                var existingEntityIds = await existingEntitiesQuery.Select(p => p.Id).ToListAsync(cancellationToken).Then(items => items.ToHashSet());

                await Util.TaskRunner.WhenAll(
                    CreateManyAsync<TEntity, TPrimaryKey>(
                        entities.Where(p => !existingEntityIds.Contains(p.Id)).ToList(),
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken),
                    UpdateManyAsync<TEntity, TPrimaryKey>(
                        entities.Where(p => existingEntityIds.Contains(p.Id)).ToList(),
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken));
            }
            else
            {
                var existingEntities = await existingEntitiesQuery.ToListAsync(cancellationToken);

                var toUpsertEntityToExistingEntityPairs = entities.SelectList(
                    toUpsertEntity =>
                    {
                        var matchedExistingEntity = existingEntities.FirstOrDefault(
                            existingEntity => customCheckExistingPredicateBuilder?.Invoke(toUpsertEntity).Compile()(existingEntity) ??
                                              existingEntity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr().Compile()(toUpsertEntity));

                        // Update to correct the id of toUpdateEntity to the matched existing entity Id
                        if (matchedExistingEntity != null) toUpsertEntity.Id = matchedExistingEntity.Id;

                        return new { toUpsertEntity, matchedExistingEntity };
                    });

                await Util.TaskRunner.WhenAll(
                    CreateManyAsync<TEntity, TPrimaryKey>(
                        toUpsertEntityToExistingEntityPairs.Where(p => p.matchedExistingEntity == null).Select(p => p.toUpsertEntity).ToList(),
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken),
                    UpdateManyAsync<TEntity, TPrimaryKey>(
                        toUpsertEntityToExistingEntityPairs.Where(p => p.matchedExistingEntity != null).Select(p => p.toUpsertEntity).ToList(),
                        dismissSendEvent,
                        eventCustomConfig,
                        cancellationToken));
            }
        }

        return entities;
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(IPlatformDbContext));
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        if (existingEntity == null &&
            ((!dismissSendEvent && entity.HasTrackValueUpdatedDomainEventAttribute()) ||
             entity is IRowVersionEntity { ConcurrencyUpdateToken: null }))
            existingEntity = await GetQuery<TEntity>()
                .Where(p => p.Id.Equals(entity.Id))
                .FirstOrDefaultAsync(cancellationToken)
                .EnsureFound($"Entity {typeof(TEntity).Name} with [Id:{entity.Id}] not found to update");

        if (entity is IRowVersionEntity { ConcurrencyUpdateToken: null })
            entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = existingEntity.As<IRowVersionEntity>().ConcurrencyUpdateToken;

        var toBeUpdatedEntity = entity
            .PipeIf(entity is IDateAuditedEntity, p => p.As<IDateAuditedEntity>().With(_ => _.LastUpdatedDate = DateTime.UtcNow).As<TEntity>())
            .PipeIf(
                entity.IsAuditedUserEntity(),
                p => p.As<IUserAuditedEntity>()
                    .SetLastUpdatedBy(UserContextAccessor.Current.UserId(userIdType: entity.GetAuditedUserIdType()))
                    .As<TEntity>());

        if (toBeUpdatedEntity is IRowVersionEntity toBeUpdatedRowVersionEntity)
        {
            var currentInMemoryConcurrencyUpdateToken = toBeUpdatedRowVersionEntity.ConcurrencyUpdateToken;
            var newUpdateConcurrencyUpdateToken = Guid.NewGuid();

            toBeUpdatedRowVersionEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;

            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, ReplaceOneResult>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeUpdatedEntity,
                existingEntity,
                entity => GetTable<TEntity>()
                    .ReplaceOneAsync(
                        p => p.Id.Equals(entity.Id) &&
                             (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                              ((IRowVersionEntity)p).ConcurrencyUpdateToken == Guid.Empty ||
                              ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken),
                        entity,
                        new ReplaceOptions { IsUpsert = false },
                        cancellationToken),
                dismissSendEvent,
                eventCustomConfig: eventCustomConfig,
                requestContext: () => UserContextAccessor.Current.GetAllKeyValues(),
                stackTrace: RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTrace(),
                cancellationToken);

            if (result.MatchedCount <= 0)
            {
                if (await GetTable<TEntity>().AsQueryable().AnyAsync(p => p.Id.Equals(toBeUpdatedEntity.Id), cancellationToken))
                    throw new PlatformDomainRowVersionConflictException(
                        $"Update {typeof(TEntity).Name} with Id:{toBeUpdatedEntity.Id} has conflicted version.");
                throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntity.Id.ToString());
            }
        }
        else
        {
            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, ReplaceOneResult>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeUpdatedEntity,
                existingEntity,
                entity => GetTable<TEntity>()
                    .ReplaceOneAsync(
                        p => p.Id.Equals(toBeUpdatedEntity.Id),
                        toBeUpdatedEntity,
                        new ReplaceOptions { IsUpsert = false },
                        cancellationToken),
                dismissSendEvent,
                eventCustomConfig: eventCustomConfig,
                requestContext: () => UserContextAccessor.Current.GetAllKeyValues(),
                stackTrace: RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTrace(),
                cancellationToken);

            if (result.MatchedCount <= 0)
                throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntity.Id.ToString());
        }

        return entity;
    }

    public virtual async Task EnsureIndexesAsync(bool recreate = false)
    {
        if (!recreate && IsEnsureIndexesMigrationExecuted()) return;

        Logger.LogInformation("[{TargetName}] EnsureIndexesAsync STARTED.", GetType().Name);

        await EnsureMigrationHistoryCollectionIndexesAsync(recreate);
        await EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(recreate);
        await EnsureInboxBusMessageCollectionIndexesAsync(recreate);
        await EnsureOutboxBusMessageCollectionIndexesAsync(recreate);
        await InternalEnsureIndexesAsync(true);

        if (!IsEnsureIndexesMigrationExecuted())
            await MigrationHistoryCollection.InsertOneAsync(
                new PlatformMongoMigrationHistory(EnsureIndexesMigrationName));

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
            ApplicationDataMigrationHistoryQuery.FirstOrDefault(p => p.Name == PlatformDataMigrationHistory.DbInitializedMigrationHistoryName)?.CreatedDate ??
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
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await MigrationHistoryCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
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

    public virtual async Task EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await ApplicationDataMigrationHistoryCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await ApplicationDataMigrationHistoryCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformDataMigrationHistory>(
                    Builders<PlatformDataMigrationHistory>.IndexKeys.Ascending(p => p.Name),
                    new CreateIndexOptions
                    {
                        Unique = true
                    }),
                new CreateIndexModel<PlatformDataMigrationHistory>(
                    Builders<PlatformDataMigrationHistory>.IndexKeys.Ascending(p => p.ConcurrencyUpdateToken)),
                new CreateIndexModel<PlatformDataMigrationHistory>(
                    Builders<PlatformDataMigrationHistory>.IndexKeys.Ascending(p => p.Status))
            ]);
    }

    public virtual async Task EnsureInboxBusMessageCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await InboxBusMessageCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await InboxBusMessageCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformInboxBusMessage>(
                    Builders<PlatformInboxBusMessage>.IndexKeys
                        .Ascending(p => p.ForApplicationName)
                        .Ascending(p => p.ConsumeStatus)
                        .Ascending(p => p.LastConsumeDate)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformInboxBusMessage>(
                    Builders<PlatformInboxBusMessage>.IndexKeys
                        .Ascending(p => p.ConsumeStatus)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformInboxBusMessage>(
                    Builders<PlatformInboxBusMessage>.IndexKeys
                        .Ascending(p => p.CreatedDate)
                        .Ascending(p => p.ConsumeStatus))
            ]);
    }

    public virtual async Task EnsureOutboxBusMessageCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await OutboxBusMessageCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await OutboxBusMessageCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PlatformOutboxBusMessage>(
                    Builders<PlatformOutboxBusMessage>.IndexKeys
                        .Ascending(p => p.SendStatus)
                        .Ascending(p => p.LastSendDate)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformOutboxBusMessage>(
                    Builders<PlatformOutboxBusMessage>.IndexKeys
                        .Ascending(p => p.SendStatus)
                        .Ascending(p => p.CreatedDate)),
                new CreateIndexModel<PlatformOutboxBusMessage>(
                    Builders<PlatformOutboxBusMessage>.IndexKeys
                        .Ascending(p => p.CreatedDate)
                        .Ascending(p => p.SendStatus))
            ]);
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

    protected bool IsEnsureIndexesMigrationExecuted()
    {
        return MigrationHistoryCollection.AsQueryable().Any(p => p.Name == EnsureIndexesMigrationName);
    }

    protected async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        bool upsert = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        var toBeCreatedEntity = entity
            .PipeIf(
                entity.IsAuditedUserEntity(),
                p => p.As<IUserAuditedEntity>()
                    .SetCreatedBy(UserContextAccessor.Current.UserId(userIdType: entity.GetAuditedUserIdType()))
                    .As<TEntity>())
            .WithIf(
                entity is IRowVersionEntity { ConcurrencyUpdateToken: null },
                entity => entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = Guid.NewGuid());

        if (upsert == false)
            await PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeCreatedEntity,
                entity => GetTable<TEntity>().InsertOneAsync(entity, null, cancellationToken).Then(() => entity),
                dismissSendEvent,
                eventCustomConfig: eventCustomConfig,
                requestContext: () => UserContextAccessor.Current.GetAllKeyValues(),
                stackTrace: RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTrace(),
                cancellationToken);
        else
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
                eventCustomConfig: eventCustomConfig,
                requestContext: () => UserContextAccessor.Current.GetAllKeyValues(),
                stackTrace: RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTrace(),
                cancellationToken);

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
            requestContext: () => UserContextAccessor.Current.GetAllKeyValues(),
            stackTrace: RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTrace(),
            cancellationToken);
    }
}
