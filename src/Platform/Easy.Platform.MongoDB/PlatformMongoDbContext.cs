using System.Data.Common;
using System.Linq.Expressions;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.MongoDB.Extensions;
using Easy.Platform.MongoDB.Migration;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Easy.Platform.MongoDB;

public abstract class PlatformMongoDbContext<TDbContext> : IPlatformDbContext
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public const string EnsureIndexesMigrationName = "EnsureIndexesAsync";
    public const string PlatformInboxBusMessageCollectionName = "InboxEventBusMessage";
    public const string PlatformOutboxBusMessageCollectionName = "OutboxEventBusMessage";
    public const string PlatformDataMigrationHistoryCollectionName = "MigrationHistory";
    public const string DbInitializedApplicationDataMigrationHistoryName = "DbInitialized";

    public readonly IMongoDatabase Database;

    protected readonly IPlatformCqrs Cqrs;
    protected readonly Lazy<Dictionary<Type, string>> EntityTypeToCollectionNameDictionary;
    protected readonly ILogger Logger;

    public PlatformMongoDbContext(
        IOptions<PlatformMongoOptions<TDbContext>> options,
        IPlatformMongoClient<TDbContext> client,
        ILoggerFactory loggerFactory,
        IPlatformCqrs cqrs)
    {
        Cqrs = cqrs;
        Database = client.MongoClient.GetDatabase(options.Value.Database);
        EntityTypeToCollectionNameDictionary = new Lazy<Dictionary<Type, string>>(BuildEntityTypeToCollectionNameDictionary);
        Logger = loggerFactory.CreateLogger(GetType());
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

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        await Migrate();
        await InsertDbInitializedApplicationDataMigrationHistory();
        await SaveChangesAsync();

        async Task InsertDbInitializedApplicationDataMigrationHistory()
        {
            if (!await ApplicationDataMigrationHistoryCollection.AsQueryable().AnyAsync(p => p.Name == DbInitializedApplicationDataMigrationHistoryName))
                await ApplicationDataMigrationHistoryCollection.InsertOneAsync(
                    new PlatformDataMigrationHistory(DbInitializedApplicationDataMigrationHistoryName));
        }
    }

    public Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.ToListAsync(cancellationToken);
    }

    public Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.FirstAsync(cancellationToken);
    }

    public Task<int> CountAsync<TEntity>(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) where TEntity : class, IEntity
    {
        return GetQuery<TEntity>().Where(predicate).CountAsync(cancellationToken);
    }

    public Task<TResult> FirstOrDefaultAsync<TEntity, TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return queryBuilder(GetQuery<TEntity>()).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return source.CountAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<TEntity>(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) where TEntity : class, IEntity
    {
        return GetQuery<TEntity>().Where(predicate).AnyAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return source.AnyAsync(cancellationToken);
    }

    public async Task<List<T>> GetAllAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return await source.ToListAsync(cancellationToken);
    }

    public async Task<T> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        return await source.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<TResult>> GetAllAsync<TEntity, TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return queryBuilder(GetQuery<TEntity>()).ToListAsync(cancellationToken);
    }

    public async Task<List<TEntity>> CreateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.Any())
        {
            await this.As<IPlatformDbContext>().EnsureEntitiesValid<TEntity, TPrimaryKey>(entities, cancellationToken);

            await GetTable<TEntity>().InsertManyAsync(entities, null, cancellationToken);

            if (!dismissSendEvent)
                await Cqrs.SendEntityEvents(entities, PlatformCqrsEntityEventCrudAction.Created, cancellationToken);
        }

        return entities;
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        var toBeUpdatedEntity = entity.PipeIf(entity is IAuditedDateEntity, p => p.As<IAuditedDateEntity>().With(_ => _.LastUpdatedDate = DateTime.UtcNow).As<TEntity>());

        if (toBeUpdatedEntity is IRowVersionEntity rowVersionEntity)
        {
            var currentInMemoryConcurrencyUpdateToken = rowVersionEntity.ConcurrencyUpdateToken;
            var newUpdateConcurrencyUpdateToken = Guid.NewGuid();

            rowVersionEntity.ConcurrencyUpdateToken = newUpdateConcurrencyUpdateToken;

            var result = await GetTable<TEntity>()
                .ReplaceOneAsync(
                    p => p.Id.Equals(toBeUpdatedEntity.Id) &&
                         (((IRowVersionEntity)p).ConcurrencyUpdateToken == null ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == Guid.Empty ||
                          ((IRowVersionEntity)p).ConcurrencyUpdateToken == currentInMemoryConcurrencyUpdateToken),
                    toBeUpdatedEntity,
                    new ReplaceOptions { IsUpsert = false },
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
            var result = await GetTable<TEntity>()
                .ReplaceOneAsync(
                    p => p.Id.Equals(toBeUpdatedEntity.Id),
                    toBeUpdatedEntity,
                    new ReplaceOptions { IsUpsert = false },
                    cancellationToken);

            if (result.MatchedCount <= 0)
                throw new PlatformDomainEntityNotFoundException<TEntity>(toBeUpdatedEntity.Id.ToString());
        }

        if (!dismissSendEvent)
            await Cqrs.SendEntityEvent(entity, PlatformCqrsEntityEventCrudAction.Updated, cancellationToken);

        return entity;
    }

    public async Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await entities.ForEachAsync(entity => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken));

        return entities;
    }

    public async Task DeleteAsync<TEntity, TPrimaryKey>(TPrimaryKey entityId, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entity = GetQuery<TEntity>().FirstOrDefault(p => p.Id.Equals(entityId));

        if (entity != null) await DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
    }

    public async Task DeleteAsync<TEntity, TPrimaryKey>(TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var result = await GetTable<TEntity>().DeleteOneAsync(p => p.Id.Equals(entity.Id), null, cancellationToken);

        if (result.DeletedCount > 0 && !dismissSendEvent)
            await Cqrs.SendEntityEvent(entity, PlatformCqrsEntityEventCrudAction.Deleted, cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entities = await GetQuery<TEntity>().Where(p => entityIds.Contains(p.Id)).ToListAsync(cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var ids = entities.Select(p => p.Id).ToList();
        await GetTable<TEntity>().DeleteManyAsync(p => ids.Contains(p.Id), cancellationToken);

        if (!dismissSendEvent)
            await Cqrs.SendEntityEvents(entities, PlatformCqrsEntityEventCrudAction.Deleted, cancellationToken);

        return entities;
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entities = await GetQuery<TEntity>().Where(predicate).ToListAsync(cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, cancellationToken);
    }

    public async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, upsert: false, cancellationToken);
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var existingEntity = await GetQuery<TEntity>()
            .When(_ => customCheckExistingPredicate != null, _ => _.Where(customCheckExistingPredicate!))
            .Else(_ => _.Where(p => p.Id.Equals(entity.Id)))
            .Execute()
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEntity != null)
        {
            entity.Id = existingEntity.Id;

            if (entity is IRowVersionEntity rowVersionEntity &&
                existingEntity is IRowVersionEntity existingRowVersionEntity)
                rowVersionEntity.ConcurrencyUpdateToken = existingRowVersionEntity.ConcurrencyUpdateToken;

            return await UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
        }

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, upsert: true, cancellationToken);
    }

    public async Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entityIds = entities.Select(p => p.Id);

        var existingEntityIds = await GetTable<TEntity>()
            .AsQueryable()
            .Where(p => entityIds.Contains(p.Id))
            .Select(p => p.Id)
            .Distinct()
            .ToListAsync(cancellationToken)
            .Then(_ => _.ToHashSet());

        var toCreateEntities = entities.Where(p => !existingEntityIds.Contains(p.Id)).ToList();
        var toUpdateEntities = entities.Where(p => existingEntityIds.Contains(p.Id)).ToList();

        await CreateManyAsync<TEntity, TPrimaryKey>(toCreateEntities, dismissSendEvent, cancellationToken);
        await UpdateManyAsync<TEntity, TPrimaryKey>(toUpdateEntities, dismissSendEvent, cancellationToken);

        return entities;
    }

    public Task SaveChangesAsync()
    {
        return Task.CompletedTask;
    }

    public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IEntity
    {
        return GetCollection<TEntity>().AsQueryable();
    }

    public async Task MigrateApplicationDataAsync(IServiceProvider serviceProvider)
    {
        PlatformDataMigrationExecutor<TDbContext>
            .EnsureAllDataMigrationExecutorsHasUniqueName(GetType().Assembly, serviceProvider);

        await PlatformDataMigrationExecutor<TDbContext>
            .GetCanExecuteDataMigrationExecutors(GetType().Assembly, serviceProvider, ApplicationDataMigrationHistoryQuery)
            .ForEachAsync(
                async migrationExecution =>
                {
                    Logger.LogInformation($"Migration {migrationExecution.Name} started.");

                    try
                    {
                        var dbInitializedMigrationHistory = ApplicationDataMigrationHistoryCollection.AsQueryable()
                            .First(p => p.Name == DbInitializedApplicationDataMigrationHistoryName);

                        if (migrationExecution.RunOnlyForDbInitializedBeforeDate == null ||
                            dbInitializedMigrationHistory.CreatedDate < migrationExecution.RunOnlyForDbInitializedBeforeDate)
                        {
                            await migrationExecution.Execute((TDbContext)this);

                            await ApplicationDataMigrationHistoryCollection.InsertOneAsync(new PlatformDataMigrationHistory(migrationExecution.Name));

                            await SaveChangesAsync();
                        }

                        migrationExecution.Dispose();
                    }
                    catch (DbException ex)
                    {
                        Logger.LogError(
                            ex,
                            "MigrateApplicationDataAsync for migration {DataMigrationName} has errors. If in dev environment it may happens if migrate cross db, when other service db is not initiated. Usually for dev environment migrate cross service db when run system in the first-time could be ignored." +
                            Environment.NewLine +
                            "Exception: {Exception}" +
                            Environment.NewLine +
                            "TrackTrace: {Exception}",
                            migrationExecution.Name,
                            ex.Message,
                            ex.StackTrace);

                        if (!PlatformEnvironment.IsDevelopment)
                            throw new Exception($"MigrateApplicationDataAsync for migration {migrationExecution.Name} has errors", ex);
                    }

                    Logger.LogInformation($"Migration {migrationExecution.Name} finished.");
                });
    }

    public void RunCommand(string command)
    {
        Database.RunCommand<BsonDocument>(command);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual async Task EnsureIndexesAsync(bool recreate = false)
    {
        if (!recreate && IsEnsureIndexesMigrationExecuted()) return;

        Logger.LogInformation($"[{GetType().Name}] EnsureIndexesAsync started.");

        await EnsureMigrationHistoryCollectionIndexesAsync(recreate);
        await EnsureApplicationDataMigrationHistoryCollectionIndexesAsync(recreate);
        await EnsureInboxBusMessageCollectionIndexesAsync(recreate);
        await EnsureOutboxBusMessageCollectionIndexesAsync(recreate);
        await InternalEnsureIndexesAsync(true);

        if (!IsEnsureIndexesMigrationExecuted())
            await MigrationHistoryCollection.InsertOneAsync(
                new PlatformMongoMigrationHistory(EnsureIndexesMigrationName));

        Logger.LogInformation($"[{GetType().Name}] EnsureIndexesAsync finished.");
    }

    public string GenerateId()
    {
        return new BsonObjectId(ObjectId.GenerateNewId()).ToString();
    }

    public async Task Migrate()
    {
        await EnsureIndexesAsync();

        EnsureAllMigrationExecutorsHasUniqueName();

        var dbInitializedDate = ApplicationDataMigrationHistoryCollection.AsQueryable()
                                    .FirstOrDefault(p => p.Name == DbInitializedApplicationDataMigrationHistoryName)
                                    ?.CreatedDate ??
                                DateTime.UtcNow;

        await NotExecutedMigrationExecutors()
            .ForEachAsync(
                async migrationExecutor =>
                {
                    if (migrationExecutor.RunOnlyDbInitializedBeforeDate == null ||
                        dbInitializedDate < migrationExecutor.RunOnlyDbInitializedBeforeDate)
                    {
                        Logger.LogInformation($"Migration {migrationExecutor.Name} started.");

                        await migrationExecutor.Execute((TDbContext)this);
                        await MigrationHistoryCollection.InsertOneAsync(new PlatformMongoMigrationHistory(migrationExecutor.Name));
                        await SaveChangesAsync();

                        Logger.LogInformation($"Migration {migrationExecutor.Name} finished.");
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

    protected bool IsEnsureIndexesMigrationExecuted()
    {
        return MigrationHistoryCollection.AsQueryable().Any(p => p.Name == EnsureIndexesMigrationName);
    }

    protected async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        bool upsert = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        if (upsert == false)
            await GetTable<TEntity>().InsertOneAsync(entity, null, cancellationToken);
        else
            await GetTable<TEntity>()
                .ReplaceOneAsync(
                    p => p.Id.Equals(entity.Id),
                    entity,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);

        if (!dismissSendEvent)
            await Cqrs.SendEntityEvent(entity, PlatformCqrsEntityEventCrudAction.Created, cancellationToken);

        return entity;
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
