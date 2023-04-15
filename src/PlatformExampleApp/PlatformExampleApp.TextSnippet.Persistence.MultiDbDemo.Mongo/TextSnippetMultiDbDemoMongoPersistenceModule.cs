using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
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

    public override List<Func<IConfiguration, Type>> ModuleTypeDependencies()
    {
        return new List<Func<IConfiguration, Type>>
        {
            p => typeof(DemoMigrateDataCrossDbPersistenceModule)
        };
    }

    // override to Config PlatformPersistenceConfiguration
    protected override PlatformPersistenceConfiguration<TextSnippetMultiDbDemoDbContext> ConfigurePersistenceConfiguration(
        PlatformPersistenceConfiguration<TextSnippetMultiDbDemoDbContext> config,
        IConfiguration configuration)
    {
        return base.ConfigurePersistenceConfiguration(config, configuration)
            .With(p => p.BadMemoryDataWarning.IsEnabled = true) 
            .With(p => p.BadMemoryDataWarning.DefaultBadMemoryDataWarningThreshold = 100) // Demo warning for getting a lot of data in to memory
            .With(p => p.BadMemoryDataWarning.IsLogWarningAsError = true) // Demo logging warning as error message
            .With(
                p => p.BadMemoryDataWarning.CustomThresholdBadMemoryDataWarningItems = Util.DictionaryBuilder.New(
                    (typeof(TextSnippetEntity), 10)));
    }
}
