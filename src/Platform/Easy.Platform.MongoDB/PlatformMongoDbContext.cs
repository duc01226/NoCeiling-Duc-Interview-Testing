using System.Linq.Expressions;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Extensions;
using Easy.Platform.MongoDB.Migration;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    public readonly IMongoDatabase Database;

    protected readonly Lazy<Dictionary<Type, string>> EntityTypeToCollectionNameDictionary;
    protected readonly PlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration;
    protected readonly IPlatformApplicationUserContextAccessor UserContextAccessor;

    public PlatformMongoDbContext(
        IOptions<PlatformMongoOptions<TDbContext>> options,
        IPlatformMongoClient<TDbContext> client,
        ILoggerFactory loggerFactory,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration)
    {
        UserContextAccessor = userContextAccessor;
        PersistenceConfiguration = persistenceConfiguration;
        Database = client.MongoClient.GetDatabase(options.Value.Database);
        EntityTypeToCollectionNameDictionary = new Lazy<Dictionary<Type, string>>(BuildEntityTypeToCollectionNameDictionary);
        Logger = CreateLogger(loggerFactory);
    }

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

    public async Task InsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity)
    {
        await ApplicationDataMigrationHistoryCollection.InsertOneAsync(entity);
    }

    public IUnitOfWork MappedUnitOfWork { get; set; }
    public ILogger Logger { get; }

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        // Store stack trace before call Migrate() to keep the original stack trace to log
        // after Migrate() will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = Environment.StackTrace;

        try
        {
            await Migrate();
            await InsertDbInitializedApplicationDataMigrationHistory();
            await SaveChangesAsync();
        }
        catch (Exception ex)
        {
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
        return GetQuery<TEntity>().WhereIf(predicate != null, predicate).CountAsync(cancellationToken);
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
        return GetQuery<TEntity>().WhereIf(predicate != null, predicate).AnyAsync(cancellationToken);
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

    public Task<List<TEntity>> CreateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .SelectAsync(entity => CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, sendEntityEventConfigure, cancellationToken)),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(result => result.SelectMany(p => p).ToList());
    }

    public Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return UpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, sendEntityEventConfigure, cancellationToken);
    }

    public Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .SelectAsync(entity => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, sendEntityEventConfigure, cancellationToken)),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(result => result.SelectMany(p => p).ToList());
    }

    public async Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entity = GetQuery<TEntity>().FirstOrDefault(p => p.Id.Equals(entityId));

        if (entity != null) await DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, sendEntityEventConfigure, cancellationToken);

        return entity;
    }

    public async Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await PlatformCqrsEntityEvent.ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey, TEntity>(
            MappedUnitOfWork,
            entity,
            async entity =>
            {
                await GetTable<TEntity>().DeleteOneAsync(p => p.Id.Equals(entity.Id), null, cancellationToken);

                return entity;
            },
            dismissSendEvent,
            hasSupportOutboxEvent: HasSupportOutboxEvent(),
            sendEntityEventConfigure: sendEntityEventConfigure,
            cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entities = await GetQuery<TEntity>().Where(p => entityIds.Contains(p.Id)).ToListAsync(cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, sendEntityEventConfigure, cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .SelectAsync(entity => DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, sendEntityEventConfigure, cancellationToken)),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(_ => _.Flatten().ToList());
    }

    public Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, upsert: false, sendEntityEventConfigure, cancellationToken);
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var existingEntity = await GetQuery<TEntity>()
            .When(_ => customCheckExistingPredicate != null, _ => _.Where(customCheckExistingPredicate!))
            .Else(_ => _.Where(p => p.Id.Equals(entity.Id)))
            .Execute()
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEntity != null)
            return await UpdateAsync<TEntity, TPrimaryKey>(
                entity.WithIf(!entity.Id.Equals(existingEntity.Id), entity => entity.Id = existingEntity.Id),
                existingEntity,
                dismissSendEvent,
                sendEntityEventConfigure,
                cancellationToken);

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, upsert: true, sendEntityEventConfigure, cancellationToken);
    }

    public Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .SelectAsync(
                        entity => CreateOrUpdateAsync<TEntity, TPrimaryKey>(
                            entity,
                            customCheckExistingPredicateBuilder?.Invoke(entity),
                            dismissSendEvent,
                            sendEntityEventConfigure,
                            cancellationToken)),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(result => result.SelectMany(p => p).ToList());
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
        return this.As<IPlatformDbContext>().MigrateApplicationDataAsync<TDbContext>(serviceProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(GetType());
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
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
                hasSupportOutboxEvent: HasSupportOutboxEvent(),
                sendEntityEventConfigure: sendEntityEventConfigure,
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
                hasSupportOutboxEvent: HasSupportOutboxEvent(),
                sendEntityEventConfigure: sendEntityEventConfigure,
                cancellationToken);

            if (result.MatchedCount <= 0)
                throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntity.Id.ToString());
        }

        return entity;
    }

    public virtual async Task EnsureIndexesAsync(bool recreate = false)
    {
        if (!recreate && IsEnsureIndexesMigrationExecuted()) return;

        Logger.LogInformation($"[{GetType().Name}] EnsureIndexesAsync STARTED.");

        await EnsureMigrationHistoryCollectionIndexesAsync(recreate);
        await EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(recreate);
        await EnsureInboxBusMessageCollectionIndexesAsync(recreate);
        await EnsureOutboxBusMessageCollectionIndexesAsync(recreate);
        await InternalEnsureIndexesAsync(true);

        if (!IsEnsureIndexesMigrationExecuted())
            await MigrationHistoryCollection.InsertOneAsync(
                new PlatformMongoMigrationHistory(EnsureIndexesMigrationName));

        Logger.LogInformation($"[{GetType().Name}] EnsureIndexesAsync FINISHED.");
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
                        Logger.LogInformation($"Migration {migrationExecutor.Name} STARTED.");

                        await migrationExecutor.Execute((TDbContext)this);
                        await MigrationHistoryCollection.InsertOneAsync(new PlatformMongoMigrationHistory(migrationExecutor.Name));
                        await SaveChangesAsync();

                        Logger.LogInformation($"Migration {migrationExecutor.Name} FINISHED.");
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

    public IMongoCollection<TEntity> GetCollection<TEntity>()
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
                new List<CreateIndexModel<PlatformMongoMigrationHistory>>
                {
                    new(
                        Builders<PlatformMongoMigrationHistory>.IndexKeys.Ascending(p => p.Name),
                        new CreateIndexOptions
                        {
                            Unique = true
                        })
                });
    }

    public virtual async Task EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await ApplicationDataMigrationHistoryCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await ApplicationDataMigrationHistoryCollection.Indexes.CreateManyAsync(
                new List<CreateIndexModel<PlatformDataMigrationHistory>>
                {
                    new(
                        Builders<PlatformDataMigrationHistory>.IndexKeys.Ascending(p => p.Name),
                        new CreateIndexOptions
                        {
                            Unique = true
                        })
                });
    }

    public virtual async Task EnsureInboxBusMessageCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await InboxBusMessageCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await InboxBusMessageCollection.Indexes.CreateManyAsync(
                new List<CreateIndexModel<PlatformInboxBusMessage>>
                {
                    new(Builders<PlatformInboxBusMessage>.IndexKeys.Ascending(p => p.RoutingKey)),
                    new(
                        Builders<PlatformInboxBusMessage>.IndexKeys
                            .Ascending(p => p.ConsumeStatus)
                            .Ascending(p => p.LastConsumeDate)),
                    new(
                        Builders<PlatformInboxBusMessage>.IndexKeys
                            .Ascending(p => p.ConsumeStatus)
                            .Ascending(p => p.NextRetryProcessAfter)
                            .Ascending(p => p.LastConsumeDate)),
                    new(
                        Builders<PlatformInboxBusMessage>.IndexKeys
                            .Descending(p => p.LastConsumeDate)
                            .Ascending(p => p.ConsumeStatus))
                });
    }

    public virtual async Task EnsureOutboxBusMessageCollectionIndexesAsync(bool recreate = false)
    {
        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await OutboxBusMessageCollection.Indexes.DropAllAsync();

        if (recreate || !IsEnsureIndexesMigrationExecuted())
            await OutboxBusMessageCollection.Indexes.CreateManyAsync(
                new List<CreateIndexModel<PlatformOutboxBusMessage>>
                {
                    new(Builders<PlatformOutboxBusMessage>.IndexKeys.Ascending(p => p.RoutingKey)),
                    new(
                        Builders<PlatformOutboxBusMessage>.IndexKeys
                            .Ascending(p => p.SendStatus)
                            .Ascending(p => p.LastSendDate)),
                    new(
                        Builders<PlatformOutboxBusMessage>.IndexKeys
                            .Descending(p => p.LastSendDate)
                            .Ascending(p => p.SendStatus)),
                    new(Builders<PlatformOutboxBusMessage>.IndexKeys.Descending(p => p.NextRetryProcessAfter))
                });
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

    protected bool HasSupportOutboxEvent()
    {
        return PlatformGlobal.RootServiceProvider.CheckHasRegisteredScopedService<IPlatformOutboxBusMessageRepository>();
    }

    protected bool IsEnsureIndexesMigrationExecuted()
    {
        return MigrationHistoryCollection.AsQueryable().Any(p => p.Name == EnsureIndexesMigrationName);
    }

    protected async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        bool upsert = false,
        Action<PlatformCqrsEntityEvent<TEntity>> sendEntityEventConfigure = null,
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
                MappedUnitOfWork,
                toBeCreatedEntity,
                entity => GetTable<TEntity>().InsertOneAsync(entity, null, cancellationToken).Then(() => entity),
                dismissSendEvent,
                hasSupportOutboxEvent: HasSupportOutboxEvent(),
                sendEntityEventConfigure: sendEntityEventConfigure,
                cancellationToken);
        else
            await PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
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
                hasSupportOutboxEvent: HasSupportOutboxEvent(),
                sendEntityEventConfigure: sendEntityEventConfigure,
                cancellationToken);

        return toBeCreatedEntity;
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    private List<PlatformMongoMigrationExecutor<TDbContext>> ScanAllMigrationExecutors()
    {
        var results = GetType()
            .Assembly.GetTypes()
            .Where(p => p.IsAssignableTo(typeof(PlatformMongoMigrationExecutor<TDbContext>)) && !p.IsAbstract)
            .Select(p => (PlatformMongoMigrationExecutor<TDbContext>)Activator.CreateInstance(p))
            .Where(p => p != null)
            .ToList();
        return results;
    }

    private void EnsureAllMigrationExecutorsHasUniqueName()
    {
        var duplicatedMigrationNames = ScanAllMigrationExecutors()
            .GroupBy(p => p.Name)
            .ToDictionary(p => p.Key, p => p.Count())
            .Where(p => p.Value > 1)
            .ToList();

        if (duplicatedMigrationNames.Any())
            throw new Exception($"Mongo Migration Executor Names is duplicated. Duplicated name: {duplicatedMigrationNames.First()}");
    }

    private List<PlatformMongoMigrationExecutor<TDbContext>> NotExecutedMigrationExecutors()
    {
        var executedMigrationNames = MigrationHistoryCollection.AsQueryable().Select(p => p.Name).ToHashSet();

        return ScanAllMigrationExecutors()
            .Where(p => !p.IsExpired())
            .OrderBy(x => x.GetOrderByValue())
            .ToList()
            .FindAll(me => !executedMigrationNames.Contains(me.Name));
    }

    private Dictionary<Type, string> BuildEntityTypeToCollectionNameDictionary()
    {
        var entityTypeToCollectionNameMaps = EntityTypeToCollectionNameMaps();
        return entityTypeToCollectionNameMaps != null ? new Dictionary<Type, string>(entityTypeToCollectionNameMaps) : null;
    }

    private static string GetPlatformEntityCollectionName<TEntity>()
    {
        if (typeof(TEntity).IsAssignableTo(typeof(PlatformInboxBusMessage)))
            return PlatformInboxBusMessageCollectionName;

        if (typeof(TEntity).IsAssignableTo(typeof(PlatformOutboxBusMessage)))
            return PlatformOutboxBusMessageCollectionName;

        if (typeof(TEntity).IsAssignableTo(typeof(PlatformMongoMigrationHistory)))
            return PlatformDataMigrationHistoryCollectionName;

        return null;
    }
}
