namespace Easy.Platform.Persistence;

public interface IPlatformPersistenceConfiguration
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public PlatformPersistenceConfigurationBadQueryWarningConfig BadQueryWarning { get; set; }

    /// <summary>
    /// Default is null.
    /// Return True to determine that the uow should not be disposed, must be kept for data has been query from it.
    /// Activate this is not optimized for the memory
    /// </summary>
    public bool? MustKeepUowForQuery { get; set; }
}

public interface IPlatformPersistenceConfiguration<TDbContext> : IPlatformPersistenceConfiguration
{
    public PlatformPersistenceConfigurationPooledDbContextOptions PooledOptions { get; set; }
}

public struct PlatformPersistenceConfigurationPooledDbContextOptions
{
    public PlatformPersistenceConfigurationPooledDbContextOptions()
    {
    }

    /// <summary>
    /// Default is true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Sets the maximum number of instances retained by the pool. Defaults to 500.
    /// </summary>
    public int PoolSize { get; set; } = 500;

    public bool UsePooledDbContextForUsingOnceTransientUowOnly { get; set; }
}

public class PlatformPersistenceConfiguration : IPlatformPersistenceConfiguration
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public virtual bool? MustKeepUowForQuery { get; set; }

    public PlatformPersistenceConfigurationBadQueryWarningConfig BadQueryWarning { get; set; } = new();
}

public class PlatformPersistenceConfiguration<TDbContext> : PlatformPersistenceConfiguration, IPlatformPersistenceConfiguration<TDbContext>
{
    public PlatformPersistenceConfigurationPooledDbContextOptions PooledOptions { get; set; }
}

public class PlatformPersistenceConfigurationBadQueryWarningConfig
{
    public bool IsEnabled { get; set; }

    public bool TotalItemsThresholdWarningEnabled { get; set; }

    /// <summary>
    /// The configuration for when count of total items data get from context into memory is equal or more than this value, the system will log warning
    /// </summary>
    public int TotalItemsThreshold { get; set; } = 100;

    /// <summary>
    /// If true, the warning log will be logged as Error level message
    /// </summary>
    public bool IsLogWarningAsError { get; set; }

    public int SlowQueryMillisecondsThreshold { get; set; } = 500;

    public int SlowWriteQueryMillisecondsThreshold { get; set; } = 2000;

    public int GetSlowQueryMillisecondsThreshold(bool forWriteQuery)
    {
        return forWriteQuery ? SlowWriteQueryMillisecondsThreshold : SlowQueryMillisecondsThreshold;
    }
}
