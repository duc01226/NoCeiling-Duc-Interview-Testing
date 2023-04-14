namespace Easy.Platform.MongoDB.Migration;

/// <summary>
/// This class is used to run migration for mongodb. Each class will be initiated and executed via Execute method.
/// The order of execution of all migration classes will be order ascending by Order then by Name;
/// </summary>
public abstract class PlatformMongoMigrationExecutor<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public abstract string Name { get; }

    /// <summary>
    /// The date that migration is expired and will never be executed
    /// </summary>
    public virtual DateTime? ExpirationDate { get; } = null;

    /// <summary>
    /// Override this prop define the date, usually the date you define your data migration. <br />
    /// When define it, for example OnlyForDbInitBeforeDate = 2000/12/31, mean that after 2000/12/31,
    /// if you run a fresh new system with no db, db is init created after 2000/12/31, the migration will be not executed.
    /// This will help to prevent run not necessary data migration for a new system fresh db
    /// </summary>
    public virtual DateTime? OnlyForDbInitBeforeDate { get; }

    public abstract Task Execute(TDbContext dbContext);

    /// <summary>
    /// Get order value string. This will be used to order migrations for execution.
    /// </summary>
    public string GetOrderByValue()
    {
        return Name;
    }

    public bool IsExpired()
    {
        return ExpirationDate.HasValue && ExpirationDate < DateTime.UtcNow;
    }
}
