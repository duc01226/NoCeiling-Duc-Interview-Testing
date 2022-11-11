using System.Data.Common;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.EfCore.EntityConfiguration;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.EfCore;

public abstract class PlatformEfCoreDbContext<TDbContext> : DbContext, IPlatformDbContext where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public const string DbInitializedApplicationDataMigrationHistoryName = "DbInitialized";

    private readonly ILogger logger;

    public PlatformEfCoreDbContext(
        DbContextOptions<TDbContext> options,
        ILoggerFactory loggerFactory) : base(options)
    {
        logger = loggerFactory.CreateLogger(GetType());
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
                    logger.LogInformation($"PlatformDataMigrationExecutor {migrationExecution.Name} started.");

                    try
                    {
                        var dbInitializedMigrationHistory = ApplicationDataMigrationHistoryDbSet.AsQueryable()
                            .First(p => p.Name == DbInitializedApplicationDataMigrationHistoryName);

                        if (!migrationExecution.IsObsolete((TDbContext)this) &&
                            (migrationExecution.RunOnlyDbInitializedBeforeDate == null ||
                             dbInitializedMigrationHistory.CreatedDate < migrationExecution.RunOnlyDbInitializedBeforeDate))
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
                            logger.LogWarning(
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

                    logger.LogInformation($"PlatformDataMigrationExecutor {migrationExecution.Name} finished.");
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

    public async Task<List<T>> GetAllAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return await query.ToListAsync(cancellationToken);
    }

    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return query.FirstOrDefaultAsync(cancellationToken);
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
