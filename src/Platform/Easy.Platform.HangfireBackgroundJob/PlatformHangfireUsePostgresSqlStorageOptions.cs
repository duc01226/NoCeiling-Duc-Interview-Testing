using Hangfire.PostgreSql;

namespace Easy.Platform.HangfireBackgroundJob;

public sealed class PlatformHangfireUsePostgreSqlStorageOptions
{
    public static readonly PostgreSqlStorageOptions DefaultStorageOptions = new();

    public string ConnectionString { get; set; }

    public PostgreSqlStorageOptions StorageOptions { get; set; } = DefaultStorageOptions;
}
