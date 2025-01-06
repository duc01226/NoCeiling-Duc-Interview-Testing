using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.EntityConfiguration;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.EfCore;

public abstract class PlatformEfCoreDbContext<TDbContext> : DbContext, IPlatformDbContext<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>, IPlatformDbContext<TDbContext>
{
    public const int ContextMaxConcurrentThreadLock = 1;

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConcurrentDictionary<string, Type> GetCachedExistingOriginalEntityCustomGetRuntimeTypeFnCachedResultDict = new();

    private readonly Lazy<ILogger> lazyLogger;
    private readonly Lazy<PlatformPersistenceConfiguration<TDbContext>> lazyPersistenceConfiguration;
    private readonly Lazy<IPlatformApplicationRequestContextAccessor> lazyRequestContextAccessor;
    private readonly Lazy<IPlatformRootServiceProvider> lazyRootServiceProvider;

    // PlatformEfCoreDbContext take only options to support context pooling factory
    public PlatformEfCoreDbContext(
        DbContextOptions<TDbContext> options) : base(options)
    {
        // Use lazy because we are using this.GetService to support EfCore pooling => force constructor must take only DbContextOptions<TDbContext>
        lazyPersistenceConfiguration = new Lazy<PlatformPersistenceConfiguration<TDbContext>>(
            () => Util.TaskRunner.CatchException(
                this.GetService<PlatformPersistenceConfiguration<TDbContext>>,
                (PlatformPersistenceConfiguration<TDbContext>)null));
        lazyRequestContextAccessor = new Lazy<IPlatformApplicationRequestContextAccessor>(this.GetService<IPlatformApplicationRequestContextAccessor>);
        lazyRootServiceProvider = new Lazy<IPlatformRootServiceProvider>(this.GetService<IPlatformRootServiceProvider>);

        // Must get loggerFactory outside lazy factory func then use it inside because when logging the context might be disposed
        // need to get logger factory here first
        var loggerFactory = Util.TaskRunner.CatchException<Exception, ILoggerFactory>(() => this.GetService<ILoggerFactory>(), fallbackValue: null);
        lazyLogger = new Lazy<ILogger>(() => CreateLogger(loggerFactory));

        IsUsingLazyLoadingProxy = options.IsUsingLazyLoadingProxy();
    }

    public bool IsUsingLazyLoadingProxy { get; set; }

    public DbSet<PlatformDataMigrationHistory> DataMigrationHistoryDbSet()
    {
        return Set<PlatformDataMigrationHistory>();
    }

    protected PlatformPersistenceConfiguration<TDbContext>? PersistenceConfiguration => lazyPersistenceConfiguration.Value;

    protected IPlatformRootServiceProvider RootServiceProvider => lazyRootServiceProvider.Value;

    protected IPlatformApplicationRequestContextAccessor RequestContextAccessor => lazyRequestContextAccessor.Value;

    protected SemaphoreSlim ContextThreadSafeLock { get; } = new(ContextMaxConcurrentThreadLock, ContextMaxConcurrentThreadLock);

    public IPlatformUnitOfWork? MappedUnitOfWork { get; set; }

    public ILogger Logger => lazyLogger.Value;

    public virtual string DbInitializedMigrationHistoryName => PlatformDataMigrationHistory.DefaultDbInitializedMigrationHistoryName;

    public Task MigrateDataAsync(IServiceProvider serviceProvider)
    {
        return this.As<IPlatformDbContext>().MigrateDataAsync<TDbContext>(serviceProvider, RootServiceProvider);
    }

    public async Task UpsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity, CancellationToken cancellationToken = default)
    {
        var existingEntity = await DataMigrationHistoryDbSet().AsNoTracking().Where(p => p.Name == entity.Name).FirstOrDefaultAsync(cancellationToken);

        if (existingEntity == null)
            await DataMigrationHistoryDbSet().AddAsync(entity, cancellationToken);
        else
        {
            if (entity is IRowVersionEntity { ConcurrencyUpdateToken: null })
                entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = existingEntity.As<IRowVersionEntity>().ConcurrencyUpdateToken;

            // Run DetachLocalIfAny to prevent
            // The instance of entity type cannot be tracked because another instance of this type with the same key is already being tracked
            var toBeUpdatedEntity = entity
                .Pipe(entity => DetachLocalIfAnyDifferentTrackedEntity(entity, p => p.Name == entity.Name, existingEntity).entity);

            DataMigrationHistoryDbSet()
                .Update(toBeUpdatedEntity)
                .Entity
                .Pipe(p => p.With(dataMigrationHistory => dataMigrationHistory.ConcurrencyUpdateToken = Ulid.NewUlid().ToString()));
        }
    }

    public IQueryable<PlatformDataMigrationHistory> DataMigrationHistoryQuery()
    {
        return DataMigrationHistoryDbSet().AsQueryable().AsNoTracking();
    }

    public async Task ExecuteWithNewDbContextInstanceAsync(Func<IPlatformDbContext, Task> fn)
    {
        await RootServiceProvider.ExecuteInjectScopedAsync(async (TDbContext context) => await fn(context));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);

            MappedUnitOfWork?.ClearCachedExistingOriginalEntity();

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            ChangeTracker.Entries()
                .Where(p => p.State == EntityState.Modified || p.State == EntityState.Added || p.State == EntityState.Deleted)
                .Select(p => p.Entity.As<IEntity>()?.GetId()?.ToString())
                .Where(p => p != null)
                .ForEach(id => MappedUnitOfWork?.RemoveCachedExistingOriginalEntity(id));
            ChangeTracker.Clear();

            throw new PlatformDomainRowVersionConflictException($"Save changes has conflicted version. {ex.Message}", ex);
        }
    }

    public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IEntity
    {
        return Set<TEntity>().AsQueryable();
    }

    public void RunCommand(string command)
    {
        Database.ExecuteSqlRaw(command);
    }

    public virtual bool DisableDbSchemaMigrateOnInitialize => false;

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        // Store stack trace before call Database.MigrateAsync() to keep the original stack trace to log
        // after Database.MigrateAsync() will lose full stack trace (may because it connects async to other external service)
        var fullStackTrace = PlatformEnvironment.StackTrace();

        try
        {
            if (!DisableDbSchemaMigrateOnInitialize) await Database.With(p => p.SetCommandTimeout(1.Days())).MigrateAsync();
            await InsertDbInitializedApplicationDataMigrationHistory();
            await SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.BeautifyStackTrace(), "PlatformEfCoreDbContext {Type} Initialize failed.", GetType().Name);

            throw new Exception(
                $"{GetType().Name} Initialize failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                ex);
        }

        async Task InsertDbInitializedApplicationDataMigrationHistory()
        {
            if (!await DataMigrationHistoryDbSet().AnyAsync(p => p.Name == DbInitializedMigrationHistoryName))
            {
                await DataMigrationHistoryDbSet()
                    .AddAsync(
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
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return entities
            .SelectAsync(
                entity => CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken))
            .ThenActionIfAsync(
                !dismissSendEvent,
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Created, eventCustomConfig, cancellationToken));
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await UpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, eventCustomConfig, cancellationToken);
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
        return await entities
            .SelectAsync(
                entity => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken))
            .ThenActionIfAsync(
                !dismissSendEvent,
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Updated, eventCustomConfig, cancellationToken));
    }

    public async Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entity = await GetQuery<TEntity>().FirstOrDefaultAsync(p => p.Id.Equals(entityId), cancellationToken);

        if (entity != null) await DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken);

        return entity;
    }

    public async Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        try
        {
            await ContextThreadSafeLock.WaitAsync(cancellationToken);

            DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(entity, null);

            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                entity,
                entity =>
                {
                    GetTable<TEntity>().Remove(entity);
                    ContextThreadSafeLock.TryRelease();

                    return Task.FromResult(entity);
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            return result;
        }
        finally
        {
            if (ContextThreadSafeLock.CurrentCount < ContextMaxConcurrentThreadLock)
                ContextThreadSafeLock.TryRelease();
        }
    }

    public async Task<List<TPrimaryKey>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entityIds.Count == 0) return entityIds;

        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
        {
            return await DeleteManyAsync<TEntity, TPrimaryKey>(p => entityIds.Contains(p.Id), true, eventCustomConfig, cancellationToken)
                .Then(() => entityIds);
        }

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
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken)
                .Then(_ => entities);
        }

        return await entities
            .SelectAsync(entity => DeleteAsync<TEntity, TPrimaryKey>(entity, false, eventCustomConfig, cancellationToken))
            .ThenActionAsync(
                entities => SendBulkEntitiesEvent<TEntity, TPrimaryKey>(entities, PlatformCqrsEntityEventCrudAction.Deleted, eventCustomConfig, cancellationToken));
    }

    public async Task<int> DeleteManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
        {
            try
            {
                await ContextThreadSafeLock.WaitAsync(cancellationToken);

                var result = await GetTable<TEntity>().Where(predicate).ExecuteDeleteAsync(cancellationToken);

                return result;
            }
            finally
            {
                ContextThreadSafeLock.TryRelease();
            }
        }

        var entities = await GetAllAsync(GetQuery<TEntity>().Where(predicate), cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, false, eventCustomConfig, cancellationToken).Then(_ => entities.Count);
    }

    public async Task<int> DeleteManyAsync<TEntity, TPrimaryKey>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
        {
            try
            {
                await ContextThreadSafeLock.WaitAsync(cancellationToken);

                var result = await queryBuilder(GetTable<TEntity>()).ExecuteDeleteAsync(cancellationToken);

                return result;
            }
            finally
            {
                ContextThreadSafeLock.TryRelease();
            }
        }

        var entities = await GetAllAsync(queryBuilder(GetQuery<TEntity>()), cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(entities, false, eventCustomConfig, cancellationToken).Then(_ => entities.Count);
    }

    public async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        try
        {
            await ContextThreadSafeLock.WaitAsync(cancellationToken);

            var toBeCreatedEntity = entity
                .Pipe(entity => DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(entity, null).entity)
                .PipeIf(
                    entity.IsAuditedUserEntity(),
                    p => p.As<IUserAuditedEntity>()
                        .SetCreatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                        .As<TEntity>())
                .WithIf(
                    entity is IRowVersionEntity { ConcurrencyUpdateToken: null },
                    entity => entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = Ulid.NewUlid().ToString());

            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeCreatedEntity,
                _ =>
                {
                    var result = GetTable<TEntity>().AddAsync(toBeCreatedEntity, cancellationToken).AsTask().Then(_ => toBeCreatedEntity);
                    ContextThreadSafeLock.TryRelease();

                    return result;
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            return result;
        }
        finally
        {
            if (ContextThreadSafeLock.CurrentCount < ContextMaxConcurrentThreadLock)
                ContextThreadSafeLock.TryRelease();
        }
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
                               .AsNoTracking()
                               .Where(existingEntityPredicate)
                               .FirstOrDefaultAsync(cancellationToken)
                               .ThenActionIf(
                                   p => p != null,
                                   p => SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p));

        if (existingEntity != null)
        {
            return await UpdateAsync<TEntity, TPrimaryKey>(
                entity.WithIf(!entity.Id.Equals(existingEntity.Id), entity => entity.Id = existingEntity.Id),
                existingEntity,
                dismissSendEvent,
                eventCustomConfig,
                cancellationToken);
        }

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken);
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
                .AsNoTracking()
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
                            .PipeAction(items => items.ForEach(p => SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p)))
                            .Pipe(existingEntities => existingEntities.Select(p => p.Id).ToHashSet()));
                var (toUpdateEntities, newEntities) = entities.WhereSplitResult(p => existingEntityIds.Contains(p.Id));

                // Ef core is not thread safe so that couldn't use when all
                await CreateManyAsync<TEntity, TPrimaryKey>(
                    newEntities,
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken);
                await UpdateManyAsync<TEntity, TPrimaryKey>(
                    toUpdateEntities,
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken);
            }
            else
            {
                var existingEntities = await existingEntitiesQuery.ToListAsync(cancellationToken)
                    .Then(
                        items => items
                            .PipeAction(items => items.ForEach(p => SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p))));

                var toUpsertEntityToExistingEntityPairs = entities.Select(
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

                // Ef core is not thread safe so that couldn't use when all
                await CreateManyAsync<TEntity, TPrimaryKey>(
                    newEntities.Select(p => p.toUpsertEntity).ToList(),
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken);
                await UpdateManyAsync<TEntity, TPrimaryKey>(
                    existingToUpdateEntities.Select(p => p.toUpsertEntity).ToList(),
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken);
            }
        }

        return entities;
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformEfCoreDbContext<>).GetNameOrGenericTypeName() + $"-{GetType().Name}");
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity? existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await InternalUpdateOrSetAsync<TEntity, TPrimaryKey>(
            entity,
            existingEntity,
            dismissSendEvent,
            eventCustomConfig,
            onlySetData: false,
            cancellationToken);
    }

    private async Task<TEntity> InternalUpdateOrSetAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig,
        bool onlySetData,
        CancellationToken cancellationToken) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        try
        {
            await ContextThreadSafeLock.WaitAsync(cancellationToken);

            var isEntityRowVersionEntityMissingConcurrencyUpdateToken = entity is IRowVersionEntity { ConcurrencyUpdateToken: null };
            existingEntity ??= MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString());

            if (existingEntity == null &&
                !onlySetData &&
                !dismissSendEvent &&
                PlatformCqrsEntityEvent.IsAnyEntityEventHandlerRegisteredForEntity<TEntity>(RootServiceProvider) &&
                entity.HasTrackValueUpdatedDomainEventAttribute())
            {
                existingEntity = await GetQuery<TEntity>()
                    .AsNoTracking()
                    .Where(BuildExistingEntityPredicate())
                    .FirstOrDefaultAsync(cancellationToken)
                    .EnsureFound($"Entity {typeof(TEntity).Name} with [Id:{entity.Id}] not found to update")
                    .ThenActionIf(
                        p => p != null,
                        p => SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(p));

                if (!existingEntity.Id.Equals(entity.Id)) entity.Id = existingEntity.Id;
            }

            if (isEntityRowVersionEntityMissingConcurrencyUpdateToken && !onlySetData)
            {
                entity.As<IRowVersionEntity>().ConcurrencyUpdateToken =
                    existingEntity?.As<IRowVersionEntity>().ConcurrencyUpdateToken ??
                    await GetQuery<TEntity>()
                        .AsNoTracking()
                        .Where(BuildExistingEntityPredicate())
                        .Select(p => ((IRowVersionEntity)p).ConcurrencyUpdateToken)
                        .FirstOrDefaultAsync(cancellationToken);
            }

            // Run DetachLocalIfAny to prevent
            // The instance of entity type cannot be tracked because another instance of this type with the same key is already being tracked
            var (toBeUpdatedEntity, isEntityTracked, isEntityNotTrackedOrTrackedModified) = entity
                .Pipe(p => DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(p, existingEntity))
                .WithIf(
                    p => p.isEntityNotTrackedOrTrackedModified && entity is IDateAuditedEntity && !onlySetData,
                    p => p.entity.As<IDateAuditedEntity>().With(auditedEntity => auditedEntity.LastUpdatedDate = DateTime.UtcNow).As<TEntity>())
                .WithIf(
                    p => p.isEntityNotTrackedOrTrackedModified && entity.IsAuditedUserEntity() && !onlySetData,
                    p => p.entity.As<IUserAuditedEntity>()
                        .SetLastUpdatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                        .As<TEntity>());

            // is entity tracked as not modified any things then return
            if (!isEntityNotTrackedOrTrackedModified)
                return entity;

            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeUpdatedEntity,
                existingEntity,
                entity =>
                {
                    var updatedEntity = !isEntityTracked || existingEntity == null
                        ? GetTable<TEntity>()
                            .Update(entity)
                            .Entity
                            .PipeIf(
                                entity is IRowVersionEntity && !onlySetData,
                                p => p.As<IRowVersionEntity>()
                                    .With(rowVersionEntity => rowVersionEntity.ConcurrencyUpdateToken = Ulid.NewUlid().ToString())
                                    .As<TEntity>())
                        : entity
                            .PipeIf(
                                entity => entity is IRowVersionEntity && !onlySetData,
                                p => p.As<IRowVersionEntity>()
                                    .With(rowVersionEntity => rowVersionEntity.ConcurrencyUpdateToken = Ulid.NewUlid().ToString())
                                    .As<TEntity>());

                    ContextThreadSafeLock.TryRelease();

                    return Task.FromResult((updatedEntity, true));
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            return result;
        }
        finally
        {
            if (ContextThreadSafeLock.CurrentCount < ContextMaxConcurrentThreadLock)
                ContextThreadSafeLock.TryRelease();
        }

        Expression<Func<TEntity, bool>> BuildExistingEntityPredicate()
        {
            return entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                ? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!
                : p => p.Id.Equals(entity.Id);
        }
    }

    public TEntity SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(TEntity p) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return MappedUnitOfWork?.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(
            p,
            false,
            p => GetCachedExistingOriginalEntityCustomGetRuntimeTypeFn(p));
    }

    public Type GetCachedExistingOriginalEntityCustomGetRuntimeTypeFn<TEntity>(TEntity entity)
        where TEntity : class, IEntity, new()
    {
        var entityType = entity.GetType();

        return GetCachedExistingOriginalEntityCustomGetRuntimeTypeFnCachedResultDict.GetOrAdd(
            $"{entityType.Name}_{typeof(TEntity)}",
            p =>
            {
                return IsUsingLazyLoadingProxy &&
                       entityType != typeof(TEntity) &&
                       entityType.GetProperties().Any(p => p.PropertyType.IsAssignableTo(typeof(ILazyLoader)))
                    ? entityType.BaseType?.IsAssignableTo(typeof(TEntity)) == true ? entityType.BaseType : typeof(TEntity)
                    : entityType;
            });
    }

    private bool IsValidScalarProperty(string propertyInfoName, Type entityType)
    {
        // Get the entity type metadata
        var efCoreEntityTypeMap = Model.FindEntityType(entityType);
        if (efCoreEntityTypeMap == null)
            return false;

        // Check if the property is a scalar (non-navigation)
        var property = efCoreEntityTypeMap.FindProperty(propertyInfoName);
        if (property == null)
            return false;

        // Ensure the property is not a navigation or ownership
        return !property.IsShadowProperty();
    }

    protected (TEntity entity, bool isEntityTracked, bool isEntityNotTrackedOrTrackedModified) DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity checkEntityModifiedByExistingEntity)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return DetachLocalIfAnyDifferentTrackedEntity(entity, entry => entry.Id.Equals(entity.Id), checkEntityModifiedByExistingEntity);
    }

    protected (TEntity entity, bool isEntityTracked, bool isEntityNotTrackedOrTrackedModified) DetachLocalIfAnyDifferentTrackedEntity<TEntity>(
        TEntity entity,
        Func<TEntity, bool> findExistingEntityPredicate,
        TEntity checkEntityModifiedByExistingEntity)
        where TEntity : class, IEntity, new()
    {
        var local = GetTable<TEntity>().Local.FirstOrDefault(findExistingEntityPredicate);

        if (local != null && !ReferenceEquals(local, entity))
            GetTable<TEntity>().Entry(local).State = EntityState.Detached;

        var isEntityTracked = local == entity;
        var isEntityTrackedModified = GetTable<TEntity>().Entry(entity!).State == EntityState.Modified;

        // Explicitly check props changes to mark it's updated to support mutate json prop like array, object, etc
        if (isEntityTracked && checkEntityModifiedByExistingEntity != null)
        {
            var changedMutableFields = entity.GetChangedFields(
                checkEntityModifiedByExistingEntity,
                p => p.PropertyType.IsMutableType() && IsValidScalarProperty(p.Name, typeof(TEntity)));

            changedMutableFields?.ForEach(p => Entry(entity!).Property(p.Key).IsModified = true);
            if (isEntityTrackedModified == false) isEntityTrackedModified = changedMutableFields?.Any() == true;
        }

        return (entity, isEntityTracked, !ReferenceEquals(local, entity) || isEntityTrackedModified);
    }

    public DbSet<TEntity> GetTable<TEntity>() where TEntity : class, IEntity, new()
    {
        return Set<TEntity>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyEntityConfigurationsFromAssembly(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlatformDataMigrationHistoryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformInboxBusMessageEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformOutboxBusMessageEntityConfiguration());
    }

    protected void ApplyEntityConfigurationsFromAssembly(ModelBuilder modelBuilder)
    {
        // Auto apply configuration by convention for the current dbcontext (usually persistence layer) assembly.
        var applyForLimitedEntityTypes = ApplyForLimitedEntityTypes();

        if (applyForLimitedEntityTypes == null && PersistenceConfiguration?.ForCrossDbMigrationOnly == true) return;

        modelBuilder.ApplyConfigurationsFromAssembly(
            GetType().Assembly,
            entityConfigType => applyForLimitedEntityTypes == null ||
                                applyForLimitedEntityTypes.Any(
                                    limitedEntityType => typeof(IEntityTypeConfiguration<>)
                                        .GetGenericTypeDefinition()
                                        .MakeGenericType(limitedEntityType)
                                        .Pipe(entityConfigType.IsAssignableTo)));
    }

    /// <summary>
    /// Override this in case you have two db context in same project, you dont want it to scan and apply entity configuration conflicted with each others. <br />
    /// return [typeof(Your Limited entity type for the db context to auto run entity configuration by scanning assembly)];
    /// </summary>
    protected virtual List<Type> ApplyForLimitedEntityTypes() { return null; }
}
