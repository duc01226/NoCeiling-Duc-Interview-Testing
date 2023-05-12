using System.Data.Common;
using System.Linq.Expressions;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.EntityConfiguration;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.EfCore;

public abstract class PlatformEfCoreDbContext<TDbContext> : DbContext, IPlatformDbContext where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public const string DbInitializedApplicationDataMigrationHistoryName = "DbInitialized";

    protected readonly IPlatformCqrs Cqrs;
    protected readonly ILogger Logger;
    protected readonly PlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration;
    protected readonly IPlatformApplicationUserContextAccessor UserContextAccessor;

    public PlatformEfCoreDbContext(
        DbContextOptions<TDbContext> options,
        ILoggerFactory loggerFactory,
        IPlatformCqrs cqrs,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        IPlatformApplicationUserContextAccessor userContextAccessor) : base(options)
    {
        Cqrs = cqrs;
        PersistenceConfiguration = persistenceConfiguration;
        UserContextAccessor = userContextAccessor;
        Logger = loggerFactory.CreateLogger(typeof(PlatformEfCoreDbContext<>));
    }

    public DbSet<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryDbSet => Set<PlatformDataMigrationHistory>();

    public IQueryable<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryQuery => ApplicationDataMigrationHistoryDbSet.AsQueryable();

    public IUnitOfWork? MappedUnitOfWork { get; set; }

    public new Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IEntity
    {
        return Set<TEntity>().AsQueryable();
    }

    public void RunCommand(string command)
    {
        Database.ExecuteSqlRaw(command);
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
                    try
                    {
                        var dbInitializedMigrationHistory = ApplicationDataMigrationHistoryDbSet.AsQueryable()
                            .First(p => p.Name == DbInitializedApplicationDataMigrationHistoryName);

                        if (dbInitializedMigrationHistory.CreatedDate < migrationExecution.CreationDate)
                        {
                            Logger.LogInformation($"PlatformDataMigrationExecutor {migrationExecution.Name} started.");

                            await migrationExecution.Execute((TDbContext)this);

                            Set<PlatformDataMigrationHistory>()
                                .Add(new PlatformDataMigrationHistory(migrationExecution.Name));

                            await base.SaveChangesAsync();

                            Logger.LogInformation($"PlatformDataMigrationExecutor {migrationExecution.Name} finished.");
                        }

                        migrationExecution.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            ex,
                            "MigrateApplicationDataAsync for migration {DataMigrationName} has errors. If in dev environment it may happens if migrate cross db, when other service db is not initiated. Usually for dev environment migrate cross service db when run system in the first-time could be ignored. " +
                            "Error: {Error}",
                            migrationExecution.Name,
                            ex.Message);

                        if (!(ex is DbException && PlatformEnvironment.IsDevelopment))
                            throw new Exception($"MigrateApplicationDataAsync for migration {migrationExecution.Name} has errors. {ex.Message}.", ex);
                    }
                });
    }

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        // Store stack trace before call Database.MigrateAsync() to keep the original stack trace to log
        // after Database.MigrateAsync() will lose full stack trace (may because it connect async to other external service)
        var stackTrace = Environment.StackTrace;

        try
        {
            await Database.MigrateAsync();
            await InsertDbInitializedApplicationDataMigrationHistory();
            await SaveChangesAsync();
        }
        catch (Exception e)
        {
            throw new Exception($"{GetType().Name} Initialize failed. {e.Message}. FullStackTrace: {stackTrace}", e);
        }

        async Task InsertDbInitializedApplicationDataMigrationHistory()
        {
            if (!await ApplicationDataMigrationHistoryDbSet
                .AnyAsync(p => p.Name == DbInitializedApplicationDataMigrationHistoryName))
                await ApplicationDataMigrationHistoryDbSet.AddAsync(
                    new PlatformDataMigrationHistory(DbInitializedApplicationDataMigrationHistoryName));
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
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .Select(entity => CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken))
                    .WhenAll(),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(result => result.SelectMany(p => p).ToList());
    }

    public Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return UpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, cancellationToken);
    }

    public Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .Select(entity => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken))
                    .WhenAll(),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(result => result.SelectMany(p => p).ToList());
    }

    public async Task DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        CancellationToken cancellationToken) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entity = GetQuery<TEntity>().FirstOrDefault(p => p.Id.Equals(entityId));

        if (entity != null) await DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
    }

    public Task DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        CancellationToken cancellationToken) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(entity);

        return PlatformCqrsEntityEvent.ExecuteWithSendingDeleteEntityEvent<TEntity, TPrimaryKey>(
            Cqrs,
            MappedUnitOfWork,
            entity,
            entity => GetTable<TEntity>().Remove(entity).ToTask(),
            dismissSendEvent,
            cancellationToken);
    }

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
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
        await Util.Pager.ExecutePagingAsync(
            (skipCount, pageSize) => entities.Skip(skipCount)
                .Take(pageSize)
                .ForEachAsync(entity => DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken)),
            maxItemCount: entities.Count,
            IPlatformDbContext.DefaultPageSize);

        return entities;
    }

    public async Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        var toBeCreatedEntity = entity
            .Pipe(DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>)
            .PipeIf(
                entity.IsAuditedUserEntity(),
                p => p.As<IUserAuditedEntity>()
                    .SetCreatedBy(UserContextAccessor.Current.UserId(userIdType: entity.GetAuditedUserIdType()))
                    .As<TEntity>())
            .WithIf(
                entity is IRowVersionEntity { ConcurrencyUpdateToken: null },
                entity => entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = Guid.NewGuid());

        var result = await PlatformCqrsEntityEvent.ExecuteWithSendingCreateEntityEvent<TEntity, TPrimaryKey, TEntity>(
            Cqrs,
            MappedUnitOfWork,
            toBeCreatedEntity,
            entity => GetTable<TEntity>().AddAsync(toBeCreatedEntity, cancellationToken).AsTask().Then(p => toBeCreatedEntity),
            dismissSendEvent,
            cancellationToken);

        return result;
    }

    public async Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var existingEntity = await GetQuery<TEntity>()
            .AsNoTracking()
            .When(_ => customCheckExistingPredicate != null, _ => _.Where(customCheckExistingPredicate!))
            .Else(_ => _.Where(p => p.Id.Equals(entity.Id)))
            .Execute()
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEntity != null)
            return await UpdateAsync<TEntity, TPrimaryKey>(entity.With(_ => _.Id = existingEntity.Id), existingEntity, dismissSendEvent, cancellationToken);

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
    }

    public Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        return Util.Pager.ExecutePagingAsync(
                (skipCount, pageSize) => entities.Skip(skipCount)
                    .Take(pageSize)
                    .Select(
                        entity => CreateOrUpdateAsync<TEntity, TPrimaryKey>(
                            entity,
                            customCheckExistingPredicateBuilder?.Invoke(entity),
                            dismissSendEvent,
                            cancellationToken))
                    .WhenAll(),
                maxItemCount: entities.Count,
                IPlatformDbContext.DefaultPageSize)
            .Then(result => result.SelectMany(p => p).ToList());
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(TEntity entity, TEntity existingEntity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        if (existingEntity == null &&
            ((!dismissSendEvent && entity.HasTrackValueUpdatedDomainEventAttribute()) || entity is IRowVersionEntity { ConcurrencyUpdateToken: null }))
            existingEntity = await GetQuery<TEntity>().AsNoTracking().Where(p => p.Id.Equals(entity.Id)).FirstOrDefaultAsync(cancellationToken);

        if (entity is IRowVersionEntity { ConcurrencyUpdateToken: null })
            entity.As<IRowVersionEntity>().ConcurrencyUpdateToken = existingEntity.As<IRowVersionEntity>().ConcurrencyUpdateToken;

        // Run DetachLocalIfAny to prevent
        // The instance of entity type cannot be tracked because another instance of this type with the same key is already being tracked
        var toBeUpdatedEntity = entity
            .Pipe(DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>)
            .PipeIf(entity is IDateAuditedEntity, p => p.As<IDateAuditedEntity>().With(_ => _.LastUpdatedDate = DateTime.UtcNow).As<TEntity>())
            .PipeIf(
                entity.IsAuditedUserEntity(),
                p => p.As<IUserAuditedEntity>()
                    .SetLastUpdatedBy(UserContextAccessor.Current.UserId(userIdType: entity.GetAuditedUserIdType()))
                    .As<TEntity>());

        var result = await PlatformCqrsEntityEvent.ExecuteWithSendingUpdateEntityEvent<TEntity, TPrimaryKey, TEntity>(
            Cqrs,
            MappedUnitOfWork,
            toBeUpdatedEntity,
            existingEntity,
            async entity =>
            {
                return GetTable<TEntity>()
                    .Update(entity)
                    .Entity
                    .PipeIf(entity is IRowVersionEntity, p => p.As<IRowVersionEntity>().With(_ => _.ConcurrencyUpdateToken = Guid.NewGuid()).As<TEntity>());
            },
            dismissSendEvent,
            cancellationToken);

        return result;
    }

    private TEntity DetachLocalIfAnyDifferentTrackedEntity<TEntity, TPrimaryKey>(TEntity entity) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var local = GetTable<TEntity>().Local.FirstOrDefault(entry => entry.Id.Equals(entity.Id));

        if (local != null && local != entity) GetTable<TEntity>().Entry(local).State = EntityState.Detached;

        return entity;
    }

    public DbSet<TEntity> GetTable<TEntity>() where TEntity : class, IEntity, new()
    {
        return Set<TEntity>();
    }

    protected bool IsEntityTracked<TEntity>(TEntity entity) where TEntity : class, IEntity, new()
    {
        return GetTable<TEntity>().Local.Any(e => e == entity);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyEntityConfigurationsFromAssembly(modelBuilder);

        modelBuilder.ApplyConfiguration(new PlatformDataMigrationHistoryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformInboxEventBusMessageEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformOutboxEventBusMessageEntityConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyEntityConfigurationsFromAssembly(ModelBuilder modelBuilder)
    {
        // Auto apply configuration by convention for the current dbcontext (usually persistence layer) assembly.
        var applyForLimitedEntityTypes = ApplyForLimitedEntityTypes();

        if (applyForLimitedEntityTypes == null && PersistenceConfiguration.ForCrossDbMigrationOnly) return;

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
