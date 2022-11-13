using Easy.Platform.MongoDB;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo;

/// <summary>
/// This is an example for using multi mixed db in one micro service.
/// We can implement an ef-core module for TextSnippetMultiDbDemoPersistencePlatformModule too
/// and import the right module as we needed.
/// </summary>
public class TextSnippetMultiDbDemoMongoPersistenceModule : PlatformMongoDbPersistenceModule<TextSnippetMultiDbDemoDbContext>
{
    public TextSnippetMultiDbDemoMongoPersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override void ConfigureMongoOptions(PlatformMongoOptions<TextSnippetMultiDbDemoDbContext> options)
    {
        options.ConnectionString = Configuration.GetSection("MongoDB:ConnectionString").Value;
        options.Database = Configuration.GetSection("MongoDB:MultiDbDemoDbDatabase").Value;
    }

    protected override List<Type> RegisterLimitedRepositoryImplementationTypes()
    {
        return new List<Type>
        {
            typeof(TextSnippetMultiDbDemoRootRepository<MultiDbDemoEntity>)
        };
    }

    protected override List<Func<IConfiguration, Type>> ModuleTypeDependencies()
    {
        return new List<Func<IConfiguration, Type>>
        {
            p => typeof(DemoMigrateDataCrossDbPersistenceModule)
        };
    }
}
