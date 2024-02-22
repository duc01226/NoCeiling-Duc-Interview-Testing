using Easy.Platform.MongoDB;
using Microsoft.Extensions.Configuration;

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
        options.ConnectionString = Configuration.GetSection("MongoDB:ConnectionString").Value;
        options.Database = Configuration.GetSection("MongoDB:Database").Value;
    }

    protected override List<Type> RegisterLimitedRepositoryImplementationTypes()
    {
        return [];
    }
}
