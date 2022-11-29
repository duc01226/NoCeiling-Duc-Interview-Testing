using System.Data.Common;
using System.Linq.Expressions;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.EfCore.EntityConfiguration;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.EfCore;

public abstract class PlatformEfCoreDbContext<TDbContext> : DbContext, IPlatformDbContext where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public const string DbInitializedApplicationDataMigrationHistoryName = "DbInitialized";

    protected readonly IPlatformCqrs Cqrs;
    protected readonly ILogger Logger;

    public PlatformEfCoreDbContext(
        DbContextOptions<TDbContext> options,
        ILoggerFactory loggerFactory,
        IPlatformCqrs cqrs) : base(options)
    {
        Cqrs = cqrs;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public DbSet<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryDbSet => Set<PlatformDataMigrationHistory>();

    public IQueryable<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryQuery => ApplicationDataMigrationHistoryDbSet.AsQueryable();

    public async Task SaveChangesAsync()
    {
        await base.SaveChangesAsync();
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
                    Logger.LogInformation($"PlatformDataMigrationExecutor {migrationExecution.Name} started.");

                    try
                    {
                        var dbInitializedMigrationHistory = ApplicationDataMigrationHistoryDbSet.AsQueryable()
                            .First(p => p.Name == DbInitializedApplicationDataMigrationHistoryName);

                        if (migrationExecution.RunOnlyForDbInitializedBeforeDate == null ||
                            dbInitializedMigrationHistory.CreatedDate < migrationExecution.RunOnlyForDbInitializedBeforeDate)
                        {
                            await migrationExecution.Execute((TDbContext)this);

                            Set<PlatformDataMigrationHistory>()
                                .Add(new PlatformDataMigrationHistory(migrationExecution.Name));

                            await base.SaveChangesAsync();
                        }

                        migrationExecution.Dispose();
                    }
                    catch (DbException ex)
                    {
                        if (PlatformEnvironment.IsDevelopment)
                            Logger.LogWarning(
                                ex,
                                "MigrateApplicationDataAsync has errors. For dev environment it may happens if migrate cross db, when other service db is not initiated. Usually for dev environment migrate cross service db when run system in the first-time could be ignored." +
                                Environment.NewLine +
                                "Exception: {Exception}" +
                                Environment.NewLine +
                                "TrackTrace: {Exception}",
                                ex.Message,
                                ex.StackTrace);
                        else throw;
                    }

                    Logger.LogInformation($"PlatformDataMigrationExecutor {migrationExecution.Name} finished.");
                });
    }

    public virtual async Task Initialize(IServiceProvider serviceProvider)
    {
        await Database.MigrateAsync();
        await InsertDbInitializedApplicationDataMigrationHistory();
        await SaveChangesAsync();

        async Task InsertDbInitializedApplicationDataMigrationHistory()
        {
            if (!await ApplicationDataMigrationHistoryDbSet
                .AnyAsync(p => p.Name == DbInitializedApplicationDataMigrationHistoryName))
                await ApplicationDataMigrationHistoryDbSet.AddAsync(
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

    public Task<TResult> FirstOrDefaultAsync<TEntity, TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return queryBuilder(GetQuery<TEntity>()).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return query.CountAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return query.AnyAsync(cancellationToken);
    }

    public async Task<List<T>> GetAllAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return await query.ToListAsync(cancellationToken);
    }

    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return query.FirstOrDefaultAsync(cancellationToken);
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

            await GetTable<TEntity>().AddRangeAsync(entities, cancellationToken);

            if (!dismissSendEvent)
                await Cqrs.SendEntityEvents(entities, PlatformCqrsEntityEventCrudAction.Created, cancellationToken);
        }

        return entities;
    }

    public async Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(TEntity entity, bool dismissSendEvent, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        var result = IsEntityTracked(entity) ? entity : GetTable<TEntity>().Update(entity).Entity;

        if (result is IRowVersionEntity rowVersionEntity)
            rowVersionEntity.ConcurrencyUpdateToken = Guid.NewGuid();

        if (!dismissSendEvent)
            await Cqrs.SendEntityEvent(entity, PlatformCqrsEntityEventCrudAction.Updated, cancellationToken);

        return result;
    }

    public async Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await entities.ForEachAsync((entity, index) => UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken));

        return entities;
    }

    public async Task DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        CancellationToken cancellationToken) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var entity = GetQuery<TEntity>().FirstOrDefault(p => p.Id.Equals(entityId));

        if (entity != null) await DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
    }

    public async Task DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        CancellationToken cancellationToken) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        GetTable<TEntity>().Remove(entity);

        await Cqrs.SendEntityEvent(entity, PlatformCqrsEntityEventCrudAction.Deleted, cancellationToken);
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
        if (entities.IsEmpty()) return entities;

        GetTable<TEntity>().RemoveRange(entities);

        if (!dismissSendEvent)
            await Cqrs.SendEntityEvents(entities, PlatformCqrsEntityEventCrudAction.Deleted, cancellationToken);

        return await entities.AsTask();
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
        await this.As<IPlatformDbContext>().EnsureEntityValid<TEntity, TPrimaryKey>(entity, cancellationToken);

        var result = await GetTable<TEntity>().AddAsync(entity, cancellationToken).AsTask().Then(p => entity);

        if (!dismissSendEvent)
            await Cqrs.SendEntityEvent(entity, PlatformCqrsEntityEventCrudAction.Created, cancellationToken);

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
        {
            entity.Id = existingEntity.Id;

            if (entity is IRowVersionEntity rowVersionEntity &&
                existingEntity is IRowVersionEntity existingRowVersionEntity)
                rowVersionEntity.ConcurrencyUpdateToken = existingRowVersionEntity.ConcurrencyUpdateToken;

            return await UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
        }

        return await CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, cancellationToken);
    }

    public async Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        if (entities.Any())
            await entities.ForEachAsync(entity => CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, customCheckExistingPredicate, dismissSendEvent, cancellationToken));

        return entities;
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
        // Auto apply configuration by convention for the current dbcontext (usually persistence layer) assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.ApplyConfiguration(new PlatformDataMigrationHistoryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformInboxEventBusMessageEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformOutboxEventBusMessageEntityConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseLazyLoadingProxies();
    }
}
