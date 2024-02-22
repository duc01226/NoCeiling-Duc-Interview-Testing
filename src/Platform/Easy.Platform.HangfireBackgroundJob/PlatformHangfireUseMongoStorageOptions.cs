using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;

namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireUseMongoStorageOptions
{
    public const string DefaultDatabaseName = "HangfireBackgroundJob";

    public static readonly MongoStorageOptions DefaultStorageOptions = new()
    {
        MigrationOptions = new MongoMigrationOptions
        {
            MigrationStrategy = new MigrateMongoMigrationStrategy(),
            BackupStrategy = new CollectionMongoBackupStrategy()
        },
        Prefix = "hangfire.mongo",
        CheckConnection = true,
        JobExpirationCheckInterval = PlatformHangfireCommonOptions.DefaultJobExpirationCheckInterval,
        CountersAggregateInterval = PlatformHangfireCommonOptions.DefaultCountersAggregateInterval,
        QueuePollInterval = PlatformHangfireCommonOptions.DefaultQueuePollInterval
    };

    public string ConnectionString { get; set; }

    public string DatabaseName { get; set; } = DefaultDatabaseName;

    public MongoStorageOptions StorageOptions { get; set; } = DefaultStorageOptions;
}
