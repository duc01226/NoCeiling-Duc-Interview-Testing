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

    public int GetBadQueryWarningTotalItemsThreshold(Type itemType);
}

public class PlatformPersistenceConfiguration : IPlatformPersistenceConfiguration
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public virtual bool? MustKeepUowForQuery { get; set; }

    public PlatformPersistenceConfigurationBadQueryWarningConfig BadQueryWarning { get; set; } = new();

    public int GetBadQueryWarningTotalItemsThreshold(Type itemType)
    {
        return BadQueryWarning.GetTotalItemsThreshold(itemType);
    }
}

public class PlatformPersistenceConfiguration<TDbContext> : PlatformPersistenceConfiguration
{
}

public class PlatformPersistenceConfigurationBadQueryWarningConfig
{
    public bool IsEnabled { get; set; }

    /// <summary>
    /// The configuration for when count of total items data get from context into memory is equal or more than DefaultBadQueryWarningThreshold, the system will log warning
    /// </summary>
    public int DefaultTotalItemsThreshold { get; set; } = 100;

    /// <summary>
    /// If true, the warning log will be logged as Error level message
    /// </summary>
    public bool IsLogWarningAsError { get; set; }

    public int SlowQueryMillisecondsThreshold { get; set; } = 500;

    public int SlowWriteQueryMillisecondsThreshold { get; set; } = 2000;

    /// <summary>
    /// Map from DataItemType => WarningThreshold
    /// </summary>
    public Dictionary<Type, int> CustomTotalItemsThresholds { get; set; } = new();

    public int GetTotalItemsThreshold(Type itemType)
    {
        if (itemType == null) return DefaultTotalItemsThreshold;

        return CustomTotalItemsThresholds.GetValueOrDefault(
            itemType,
            defaultValue: DefaultTotalItemsThreshold);
    }

    public int GetSlowQueryMillisecondsThreshold(bool forWriteQuery)
    {
        return forWriteQuery ? SlowWriteQueryMillisecondsThreshold : SlowQueryMillisecondsThreshold;
    }
}
