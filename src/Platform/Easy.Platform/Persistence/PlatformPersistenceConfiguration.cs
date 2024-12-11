using Easy.Platform.Common.Utils;

namespace Easy.Platform.Persistence;

public interface IPlatformPersistenceConfiguration
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public PlatformPersistenceConfigurationBadQueryWarningConfig BadQueryWarning { get; set; }

    public bool EnableDebugQueryLog { get; set; }
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
    /// Sets the maximum number of instances retained by the pool.
    /// </summary>
    public int PoolSize { get; set; } = Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent * Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent;
}

public class PlatformPersistenceConfiguration : IPlatformPersistenceConfiguration
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public PlatformPersistenceConfigurationBadQueryWarningConfig BadQueryWarning { get; set; } = new();

    /// <summary>
    /// If true, query log in Debugger when it's attached, mean that you are debugging, is logout in the output of debugger
    /// </summary>
    public bool EnableDebugQueryLog { get; set; } = true;
}

public class PlatformPersistenceConfiguration<TDbContext> : PlatformPersistenceConfiguration, IPlatformPersistenceConfiguration<TDbContext>
{
    public PlatformPersistenceConfigurationPooledDbContextOptions PooledOptions { get; set; }
}

/// <summary>
/// Support log warning for slow query in the application. Aware that if enable this feature, performance of the application might be affected
/// It will log the data count and also get stack trace to log the slow query
/// </summary>
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
