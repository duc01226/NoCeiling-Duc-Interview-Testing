namespace Easy.Platform.Persistence;

public class PlatformPersistenceConfiguration<TDbContext>
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public BadQueryWarningConfig BadQueryWarning { get; set; } = new();

    public int GetBadQueryWarningTotalItemsThreshold<TSource>()
    {
        return BadQueryWarning.GetTotalItemsThreshold<TSource>();
    }

    public class BadQueryWarningConfig
    {
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The configuration for when count of total items data get from context into memory is equal or more than DefaultBadQueryWarningThreshold, the system will log warning
        /// </summary>
        public int DefaultTotalItemsThreshold { get; set; } = 1000;

        /// <summary>
        /// If true, the warning log will be logged as Error level message
        /// </summary>
        public bool IsLogWarningAsError { get; set; }

        public int SlowQueryMillisecondsThreshold { get; set; } = 1000;

        /// <summary>
        /// Map from DataItemType => WarningThreshold
        /// </summary>
        public Dictionary<Type, int> CustomTotalItemsThresholds { get; set; } = new();

        public int GetTotalItemsThreshold<TSource>()
        {
            return CustomTotalItemsThresholds.GetValueOrDefault(
                typeof(TSource),
                defaultValue: DefaultTotalItemsThreshold);
        }
    }
}
