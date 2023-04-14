using System.Reflection;
using Easy.Platform.Application.Persistence;

namespace Easy.Platform.Persistence.DataMigration;

/// <summary>
/// This interface is used for conventional registration for IPlatformDataMigrationExecutor[TDbContext]
/// </summary>
public interface IPlatformDataMigrationExecutor : IDisposable
{
}

public interface IPlatformDataMigrationExecutor<in TDbContext> : IPlatformDataMigrationExecutor
    where TDbContext : IPlatformDbContext
{
    /// <summary>
    /// The unique name of the migration. The name will be used to order. Convention should be: YYYYMMDDhhmmss_MigrationName
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Set this data to state that the data migration only valid if db initialized before a certain date. <br />
    /// Implement this prop define the date, usually the date you define your data migration. <br />
    /// When define it, for example CreationDate = 2000/12/31, mean that after 2000/12/31,
    /// if you run a fresh new system with no db, db is init created after 2000/12/31, the migration will be not executed.
    /// This will help to prevent run not necessary data migration for a new system fresh db
    /// </summary>
    DateTime CreationDate { get; }

    /// <summary>
    /// The find the date that this migration will not be executed after a given date.
    /// </summary>
    DateTime? ExpirationDate { get; }

    Task Execute(TDbContext dbContext);

    bool IsExpired();

    /// <summary>
    /// Get order value string. This will be used to order migrations for execution.
    /// <br />
    /// Example: "00001_MigrationName"
    /// </summary>
    string GetOrderByValue();
}

/// <summary>
/// This class is used to run APPLICATION DATA migration, when you need to migrate your data in your whole micro services application.
/// Each class will be initiated and executed via Execute method.
/// The order of execution of all migration classes will be order ascending by Order then by Name;
/// </summary>
public abstract class PlatformDataMigrationExecutor<TDbContext> : IPlatformDataMigrationExecutor<TDbContext>
    where TDbContext : IPlatformDbContext
{
    public abstract string Name { get; }

    public virtual DateTime? ExpirationDate => null;

    public abstract DateTime CreationDate { get; }

    public abstract Task Execute(TDbContext dbContext);

    public bool IsExpired()
    {
        return ExpirationDate.HasValue && ExpirationDate < DateTime.UtcNow;
    }

    /// <summary>
    /// Get order value string. This will be used to order migrations for execution.
    /// <br />
    /// Example: "00001_MigrationName"
    /// </summary>
    public string GetOrderByValue()
    {
        return Name;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public static List<PlatformDataMigrationExecutor<TDbContext>> ScanAllDataMigrationExecutors(
        Assembly scanAssembly,
        IServiceProvider serviceProvider)
    {
        var results = scanAssembly.GetTypes()
            .Where(p => p.IsAssignableTo(typeof(PlatformDataMigrationExecutor<TDbContext>)) && !p.IsAbstract)
            .Select(p => (PlatformDataMigrationExecutor<TDbContext>)serviceProvider.GetService(p))
            .Where(p => p != null)
            .ToList();
        return results;
    }

    public static void EnsureAllDataMigrationExecutorsHasUniqueName(
        Assembly scanAssembly,
        IServiceProvider serviceProvider)
    {
        var allDataMigrationExecutors = ScanAllDataMigrationExecutors(scanAssembly, serviceProvider);

        var applicationDataMigrationExecutionNames = new HashSet<string>();

        allDataMigrationExecutors.ForEach(
            dataMigrationExecutor =>
            {
                if (applicationDataMigrationExecutionNames.Contains(dataMigrationExecutor.Name))
                    throw new Exception(
                        $"Application Data Migration Executor Names is duplicated. Duplicated name: {dataMigrationExecutor.Name}");

                applicationDataMigrationExecutionNames.Add(dataMigrationExecutor.Name);

                dataMigrationExecutor.Dispose();
            });
    }

    public static List<PlatformDataMigrationExecutor<TDbContext>> GetCanExecuteDataMigrationExecutors(
        Assembly scanAssembly,
        IServiceProvider serviceProvider,
        IQueryable<PlatformDataMigrationHistory> allApplicationDataMigrationHistoryQuery)
    {
        var executedMigrationNames = allApplicationDataMigrationHistoryQuery.Select(p => p.Name).ToHashSet();

        var canExecutedMigrations = new List<PlatformDataMigrationExecutor<TDbContext>>();

        ScanAllDataMigrationExecutors(scanAssembly, serviceProvider)
            .OrderBy(x => x.GetOrderByValue())
            .ToList()
            .ForEach(
                migrationExecution =>
                {
                    if (!executedMigrationNames.Contains(migrationExecution.Name) &&
                        !migrationExecution.IsExpired())
                        canExecutedMigrations.Add(migrationExecution);
                    else
                        migrationExecution.Dispose();
                });

        return canExecutedMigrations;
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
