using Hangfire.SqlServer;

namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireUseSqlServerStorageOptions
{
    public static readonly SqlServerStorageOptions DefaultStorageOptions = new()
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = PlatformHangfireCommonOptions.DefaultQueuePollInterval,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        JobExpirationCheckInterval = PlatformHangfireCommonOptions.DefaultJobExpirationCheckInterval,
        CountersAggregateInterval = PlatformHangfireCommonOptions.DefaultCountersAggregateInterval
    };

    public string ConnectionString { get; set; }

    public SqlServerStorageOptions StorageOptions { get; set; } = DefaultStorageOptions;
}
