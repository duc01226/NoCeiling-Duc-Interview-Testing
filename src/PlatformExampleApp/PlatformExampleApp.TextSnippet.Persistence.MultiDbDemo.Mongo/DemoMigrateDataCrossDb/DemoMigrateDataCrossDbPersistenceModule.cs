using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

/// <summary>
/// This is an example for using declare context to connect to other db to do data cross db migrations
/// </summary>
public class DemoMigrateDataCrossDbPersistenceModule : PlatformMongoDbPersistenceModule<DemoMigrateDataCrossDbContext>
{
    public DemoMigrateDataCrossDbPersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public override bool ForCrossDbMigrationOnly => true;

    protected override void ConfigureMongoOptions(PlatformMongoOptions<DemoMigrateDataCrossDbContext> options)
    {
        options.ConnectionString = new MongoUrlBuilder(Configuration.GetSection("MongoDB:ConnectionString").Value)
            .With(
                p => p.MinConnectionPoolSize = RecommendedMinPoolSize) // Always available connection to serve request, reduce latency
            .With(p => p.MaxConnectionPoolSize = RecommendedMaxPoolSize)
            //.With(p => p.MaxConnectionIdleTime = RecommendedConnectionIdleLifetimeSeconds.Seconds())
            .ToString();
        options.Database = Configuration.GetSection("MongoDB:Database").Value;
    }

    protected override List<Type> RegisterLimitedRepositoryImplementationTypes()
    {
        return [];
    }
}
