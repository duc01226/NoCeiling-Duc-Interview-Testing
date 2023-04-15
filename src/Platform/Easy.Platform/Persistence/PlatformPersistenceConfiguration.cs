namespace Easy.Platform.Persistence;

public class PlatformPersistenceConfiguration<TDbContext>
{
    public bool ForCrossDbMigrationOnly { get; set; }

    public BadMemoryDataWarningConfig BadMemoryDataWarning { get; set; } = new();

    public int GetBadMemoryDataWarningThreshold<TSource>()
    {
        return BadMemoryDataWarning.GetBadMemoryDataWarningThreshold<TSource>();
    }

    public class BadMemoryDataWarningConfig
    {
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The configuration for when count of total items data get from context into memory is equal or more than DefaultBadMemoryDataWarningThreshold, the system will log warning
        /// </summary>
        public int DefaultBadMemoryDataWarningThreshold { get; set; } = 1000;

        /// <summary>
        /// If true, the warning log will be logged as Error level message
        /// </summary>
        public bool IsLogWarningAsError { get; set; }

        /// <summary>
        /// Map from DataItemType => WarningThreshold
        /// </summary>
        public Dictionary<Type, int> CustomThresholdBadMemoryDataWarningItems { get; set; } = new();

        public int GetBadMemoryDataWarningThreshold<TSource>()
        {
            return CustomThresholdBadMemoryDataWarningItems.GetValueOrDefault(
                typeof(TSource),
                defaultValue: DefaultBadMemoryDataWarningThreshold);
        }
    }
}
