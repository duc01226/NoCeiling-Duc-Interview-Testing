using System.Linq.Expressions;
using Easy.Platform.Application;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.EfCore;

public abstract class PlatformEfCoreDbContext<TDbContext> : DbContext, IPlatformDbContext<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>, IPlatformDbContext<TDbContext>
{
    public const int ContextMaxConcurrentThreadLock = 1;

    private readonly Lazy<IPlatformApplicationSettingContext> applicationSettingContext;
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
        lazyLogger = new Lazy<ILogger>(() => CreateLogger(this.GetService<ILoggerFactory>()));
        applicationSettingContext = new Lazy<IPlatformApplicationSettingContext>(() => lazyRootServiceProvider.Value.GetService<IPlatformApplicationSettingContext>());
    }

    public PlatformEfCoreDbContext(
        DbContextOptions<TDbContext> options,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        IPlatformRootServiceProvider rootServiceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext) : base(options)
    {
        // Use lazy because we are using this.GetService to support EfCore pooling => force constructor must take only DbContextOptions<TDbContext>
        lazyPersistenceConfiguration = new Lazy<PlatformPersistenceConfiguration<TDbContext>>(() => persistenceConfiguration);
        lazyRequestContextAccessor = new Lazy<IPlatformApplicationRequestContextAccessor>(() => requestContextAccessor);
        lazyRootServiceProvider = new Lazy<IPlatformRootServiceProvider>(() => rootServiceProvider);
        lazyLogger = new Lazy<ILogger>(() => CreateLogger(loggerFactory));
        this.applicationSettingContext = new Lazy<IPlatformApplicationSettingContext>(() => applicationSettingContext);
    }

    public DbSet<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryDbSet => Set<PlatformDataMigrationHistory>();

    protected PlatformPersistenceConfiguration<TDbContext>? PersistenceConfiguration => lazyPersistenceConfiguration.Value;

    protected IPlatformRootServiceProvider RootServiceProvider => lazyRootServiceProvider.Value;

    protected IPlatformApplicationRequestContextAccessor RequestContextAccessor => lazyRequestContextAccessor.Value;

    protected SemaphoreSlim NotThreadSafeDbContextQueryLock { get; } = new(ContextMaxConcurrentThreadLock, ContextMaxConcurrentThreadLock);

    public IPlatformUnitOfWork? MappedUnitOfWork { get; set; }

    public ILogger Logger => lazyLogger.Value;

    public Task MigrateApplicationDataAsync(IServiceProvider serviceProvider)
    {
        return this.As<IPlatformDbContext>().MigrateApplicationDataAsync<TDbContext>(serviceProvider, RootServiceProvider);
    }

    public IQueryable<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryQuery => ApplicationDataMigrationHistoryDbSet.AsQueryable();

    public async Task UpsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity, CancellationToken cancellationToken = default)
    {
        var existingEntity = await ApplicationDataMigrationHistoryDbSet.AsNoTracking().Where(p => p.Name == entity.Name).FirstOrDefaultAsync(cancellationToken);

        if (existingEntity == null)
        {
            await ApplicationDataMigrationHistoryDbSet.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity is IRowVersionEntity { ConcurrencyUpdateToken: null })
                entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = existingEntity.As<IRowVersionEntity>().ConcurrencyUpdateToken;

            // Run DetachLocalIfAny to prevent
            // The instance of entity type cannot be tracked because another instance of this type with the same key is already being tracked
            var toBeUpdatedEntity = entity
                .Pipe(entity => DetachLocalIfAnyDifferentTrackedEntity(entity, p => p.Name == entity.Name));

            ApplicationDataMigrationHistoryDbSet
                .Update(toBeUpdatedEntity)
                .Entity
                .Pipe(p => p.With(dataMigrationHistory => dataMigrationHistory.ConcurrencyUpdateToken = Ulid.NewUlid().ToString()));
        }
    }

    public IQueryable<PlatformDataMigrationHistory> DataMigrationHistoryQuery()
    {
        return ApplicationDataMigrationHistoryDbSet.AsQueryable().AsNoTracking();
    }

    public async Task ExecuteWithNewDbContextInstanceAsync(Func<IPlatformDbContext, Task> fn)
    {
        await RootServiceProvider.ExecuteInjectScopedAsync(async (TDbContext context) => await fn(context));
    }

    public new async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await base.SaveChangesAsync(cancellationToken);

            MappedUnitOfWork?.ClearCachedExistingOriginalEntity();
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

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        // Store stack trace before call Database.MigrateAsync() to keep the original stack trace to log
        // after Database.MigrateAsync() will lose full stack trace (may because it connects async to other external service)
        var fullStackTrace = PlatformEnvironment.StackTrace();

        try
        {
            await Database.MigrateAsync();
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
            if (!await ApplicationDataMigrationHistoryDbSet.AnyAsync(p => p.Name == PlatformDataMigrationHistory.DbInitializedMigrationHistoryName))
                await ApplicationDataMigrationHistoryDbSet.AddAsync(
                    new PlatformDataMigrationHistory(PlatformDataMigrationHistory.DbInitializedMigrationHistoryName)
                    {
                        Status = PlatformDataMigrationHistory.Statuses.Processed
                    });
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
        return entities.SelectAsync(entity => CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken))
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
        try
        {
            await NotThreadSafeDbContextQueryLock.WaitAsync(cancellationToken);

            // Run DetachLocalIfAny to prevent
            // The instance of entity type cannot be tracked because another instance of this type with the same key is already being tracked
            var toBeUpdatedEntity = entity.Pipe(DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>);

            var result = GetTable<TEntity>()
                .Update(toBeUpdatedEntity)
                .Entity;

            NotThreadSafeDbContextQueryLock.Release();

            return result;
        }
        catch
        {
            if (NotThreadSafeDbContextQueryLock.CurrentCount < ContextMaxConcurrentThreadLock)
                NotThreadSafeDbContextQueryLock.Release();
            throw;
        }
    }

    public async Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return await entities.SelectAsync(entity => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken))
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
        var entity = GetQuery<TEntity>().FirstOrDefault(p => p.Id.Equals(entityId));

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
            await NotThreadSafeDbContextQueryLock.WaitAsync(cancellationToken);

            DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(entity);

            return await PlatformCqrsEntityEvent.ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                entity,
                entity =>
                {
                    GetTable<TEntity>().Remove(entity);

                    NotThreadSafeDbContextQueryLock.Release();

                    return Task.FromResult(entity);
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(IgnoreLogRequestContextKeys()),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);
        }
        catch (Exception)
        {
            if (NotThreadSafeDbContextQueryLock.CurrentCount < ContextMaxConcurrentThreadLock)
                NotThreadSafeDbContextQueryLock.Release();
            throw;
        }
    }

    public async Task<List<TPrimaryKey>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (dismissSendEvent || !PlatformCqrsEntityEvent.IsAnyKindsOfEventHandlerRegisteredForEntity<TEntity, TPrimaryKey>(RootServiceProvider))
            return await DeleteManyAsync<TEntity, TPrimaryKey>(p => entityIds.Contains(p.Id), true, eventCustomConfig, cancellationToken)
                .Then(() => entityIds);

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
            try
            {
                await NotThreadSafeDbContextQueryLock.WaitAsync(cancellationToken);

                return await GetTable<TEntity>().Where(predicate).ExecuteDeleteAsync(cancellationToken);
            }
            finally
            {
                NotThreadSafeDbContextQueryLock.Release();
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
            try
            {
                await NotThreadSafeDbContextQueryLock.WaitAsync(cancellationToken);

                return await queryBuilder(GetTable<TEntity>()).ExecuteDeleteAsync(cancellationToken);
            }
            finally
            {
                NotThreadSafeDbContextQueryLock.Release();
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
            await NotThreadSafeDbContextQueryLock.WaitAsync(cancellationToken);

            var toBeCreatedEntity = entity
                .Pipe(DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>)
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

                    NotThreadSafeDbContextQueryLock.Release();

                    return result;
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(IgnoreLogRequestContextKeys()),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            return result;
        }
        catch (Exception)
        {
            if (NotThreadSafeDbContextQueryLock.CurrentCount < ContextMaxConcurrentThreadLock)
                NotThreadSafeDbContextQueryLock.Release();
            throw;
        }
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var existingEntityPredicate = customCheckExistingPredicate != null ||
                                      entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
            ? customCheckExistingPredicate ?? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!
            : p => p.Id.Equals(entity.Id);

        var existingEntity = MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()) ??
                             await GetQuery<TEntity>()
                                 .AsNoTracking()
                                 .Where(existingEntityPredicate)
                                 .FirstOrDefaultAsync(cancellationToken)
                                 .ThenActionIf(
                                     p => p != null && MappedUnitOfWork?.CreatedByUnitOfWorkManager.HasCurrentActiveUow() == true,
                                     p => MappedUnitOfWork?.SetCachedExistingOriginalEntity(p));

        if (existingEntity != null)
            return await UpdateAsync<TEntity, TPrimaryKey>(
                entity.WithIf(!entity.Id.Equals(existingEntity.Id), entity => entity.Id = existingEntity.Id),
                existingEntity,
                dismissSendEvent,
                eventCustomConfig,
                cancellationToken);

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
                var existingEntityIds = await existingEntitiesQuery.Select(p => p.Id).ToListAsync(cancellationToken).Then(items => items.ToHashSet());
                var (existingEntities, newEntities) = entities.WhereSplitResult(p => existingEntityIds.Contains(p.Id));

                // Ef core is not thread safe so that couldn't use when all
                await CreateManyAsync<TEntity, TPrimaryKey>(
                    newEntities,
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken);
                await UpdateManyAsync<TEntity, TPrimaryKey>(
                    existingEntities,
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken);
            }
            else
            {
                var existingEntities = await existingEntitiesQuery.ToListAsync(cancellationToken);

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

    protected HashSet<string> IgnoreLogRequestContextKeys()
    {
        return applicationSettingContext.Value.GetIgnoreRequestContextKeys();
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(IPlatformDbContext).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}");
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity? existingEntity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        try
        {
            await NotThreadSafeDbContextQueryLock.WaitAsync(cancellationToken);

            var isEntityRowVersionEntityMissingConcurrencyUpdateToken = entity is IRowVersionEntity { ConcurrencyUpdateToken: null };

            if (existingEntity == null &&
                !dismissSendEvent &&
                PlatformCqrsEntityEvent.IsAnyEntityEventHandlerRegisteredForEntity<TEntity>(RootServiceProvider) &&
                entity.HasTrackValueUpdatedDomainEventAttribute())
            {
                existingEntity = MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()) ??
                                 await GetQuery<TEntity>()
                                     .AsNoTracking()
                                     .Where(BuildExistingEntityPredicate())
                                     .FirstOrDefaultAsync(cancellationToken)
                                     .EnsureFound($"Entity {typeof(TEntity).Name} with [Id:{entity.Id}] not found to update");

                if (!existingEntity.Id.Equals(entity.Id)) entity.Id = existingEntity.Id;
            }

            if (isEntityRowVersionEntityMissingConcurrencyUpdateToken)
                entity.As<IRowVersionEntity>().ConcurrencyUpdateToken =
                    existingEntity?.As<IRowVersionEntity>().ConcurrencyUpdateToken ??
                    await GetQuery<TEntity>()
                        .AsNoTracking()
                        .Where(BuildExistingEntityPredicate())
                        .Select(p => ((IRowVersionEntity)p).ConcurrencyUpdateToken)
                        .FirstOrDefaultAsync(cancellationToken);

            // Run DetachLocalIfAny to prevent
            // The instance of entity type cannot be tracked because another instance of this type with the same key is already being tracked
            var toBeUpdatedEntity = entity
                .Pipe(DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>)
                .PipeIf(
                    entity is IDateAuditedEntity,
                    p => p.As<IDateAuditedEntity>().With(auditedEntity => auditedEntity.LastUpdatedDate = DateTime.UtcNow).As<TEntity>())
                .PipeIf(
                    entity.IsAuditedUserEntity(),
                    p => p.As<IUserAuditedEntity>()
                        .SetLastUpdatedBy(RequestContextAccessor.Current.UserId(entity.GetAuditedUserIdType()))
                        .As<TEntity>());

            var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TEntity>(
                RootServiceProvider,
                MappedUnitOfWork,
                toBeUpdatedEntity,
                existingEntity ?? MappedUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entity.Id.ToString()),
                entity =>
                {
                    var result = GetTable<TEntity>()
                        .Update(entity)
                        .Entity
                        .PipeIf(
                            entity is IRowVersionEntity,
                            p => p.As<IRowVersionEntity>().With(rowVersionEntity => rowVersionEntity.ConcurrencyUpdateToken = Ulid.NewUlid().ToString()).As<TEntity>());

                    NotThreadSafeDbContextQueryLock.Release();

                    return Task.FromResult(result);
                },
                dismissSendEvent,
                eventCustomConfig,
                () => RequestContextAccessor.Current.GetAllKeyValues(IgnoreLogRequestContextKeys()),
                PlatformCqrsEntityEvent.GetEntityEventStackTrace<TEntity>(RootServiceProvider, dismissSendEvent),
                cancellationToken);

            return result;
        }
        catch
        {
            if (NotThreadSafeDbContextQueryLock.CurrentCount < ContextMaxConcurrentThreadLock)
                NotThreadSafeDbContextQueryLock.Release();
            throw;
        }

        Expression<Func<TEntity, bool>> BuildExistingEntityPredicate()
        {
            return entity.As<IUniqueCompositeIdSupport<TEntity>>()?.FindByUniqueCompositeIdExpr() != null
                ? entity.As<IUniqueCompositeIdSupport<TEntity>>().FindByUniqueCompositeIdExpr()!
                : p => p.Id.Equals(entity.Id);
        }
    }

    protected TEntity DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(TEntity entity) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return DetachLocalIfAnyDifferentTrackedEntity(entity, entry => entry.Id.Equals(entity.Id));
    }

    protected TEntity DetachLocalIfAnyDifferentTrackedEntity<TEntity>(TEntity entity, Func<TEntity, bool> findExistingEntityPredicate)
        where TEntity : class, IEntity, new()
    {
        var local = GetTable<TEntity>().Local.FirstOrDefault(findExistingEntityPredicate);

        if (local != null && local != entity) GetTable<TEntity>().Entry(local).State = EntityState.Detached;

        return entity;
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
            () => RequestContextAccessor.Current.GetAllKeyValues(IgnoreLogRequestContextKeys()),
            PlatformCqrsEntityEvent.GetBulkEntitiesEventStackTrace<TEntity, TPrimaryKey>(RootServiceProvider),
            cancellationToken);
    }

    protected bool IsEntityTracked<TEntity>(TEntity entity) where TEntity : class, IEntity, new()
    {
        return GetTable<TEntity>().Local.Any(e => e == entity);
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
    /// return Util.ListBuilder.New(typeof(Your Limited entity type for the db context to auto run entity configuration by scanning assembly));
    /// </summary>
    protected virtual List<Type> ApplyForLimitedEntityTypes() { return null; }
}
