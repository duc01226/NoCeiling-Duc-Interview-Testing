using Hangfire.SqlServer;

namespace Easy.Platform.HangfireBackgroundJob;

public sealed class PlatformHangfireUseSqlServerStorageOptions
{
    public static readonly SqlServerStorageOptions DefaultStorageOptions = new()
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    };

    public string ConnectionString { get; set; }

    public SqlServerStorageOptions StorageOptions { get; set; } = DefaultStorageOptions;
}
