using Hangfire.SqlServer;

namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireUseSqlServerStorageOptions
{
    public static readonly SqlServerStorageOptions DefaultStorageOptions = new()
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        JobExpirationCheckInterval = PlatformHangfireCommonOptions.DefaultJobExpirationCheckInterval
    };

    public string ConnectionString { get; set; }

    public SqlServerStorageOptions StorageOptions { get; set; } = DefaultStorageOptions;
}
