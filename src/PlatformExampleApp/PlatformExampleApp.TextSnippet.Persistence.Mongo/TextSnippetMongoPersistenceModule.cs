using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Application;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo;

public class TextSnippetMongoPersistenceModule : PlatformMongoDbPersistenceModule<TextSnippetDbContext>
{
    public TextSnippetMongoPersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override void ConfigureMongoOptions(PlatformMongoOptions<TextSnippetDbContext> options)
    {
        options.ConnectionString = Configuration.GetSection("MongoDB:ConnectionString").Value;
        options.Database = Configuration.GetSection("MongoDB:Database").Value;
        options.MinConnectionPoolSize =
            Configuration.GetValue<int?>("MongoDB:MinConnectionPoolSize") ?? TextSnippetApplicationConstants.DefaultBackgroundJobWorkerCount + 2;
    }

    protected override bool EnableInboxBusMessage()
    {
        return true;
    }

    protected override bool EnableOutboxBusMessage()
    {
        return true;
    }

    // override to Config PlatformPersistenceConfiguration
    protected override PlatformPersistenceConfiguration<TextSnippetDbContext> ConfigurePersistenceConfiguration(
        PlatformPersistenceConfiguration<TextSnippetDbContext> config,
        IConfiguration configuration)
    {
        return base.ConfigurePersistenceConfiguration(config, configuration)
            .With(p => p.BadQueryWarning.IsEnabled = configuration.GetValue<bool>("PersistenceConfiguration:BadQueryWarning:IsEnabled"))
            .With(p => p.BadQueryWarning.TotalItemsThresholdWarningEnabled = true)
            .With(
                p => p.BadQueryWarning.TotalItemsThreshold =
                    configuration.GetValue<int>("PersistenceConfiguration:BadQueryWarning:TotalItemsThreshold")) // Demo warning for getting a lot of data in to memory
            .With(
                p => p.BadQueryWarning.SlowQueryMillisecondsThreshold =
                    configuration.GetValue<int>("PersistenceConfiguration:BadQueryWarning:SlowQueryMillisecondsThreshold"))
            .With(
                p => p.BadQueryWarning.SlowWriteQueryMillisecondsThreshold =
                    configuration.GetValue<int>("PersistenceConfiguration:BadQueryWarning:SlowWriteQueryMillisecondsThreshold"))
            .With(
                p => p.BadQueryWarning.IsLogWarningAsError =
                    configuration.GetValue<bool>("PersistenceConfiguration:BadQueryWarning:IsLogWarningAsError")); // Demo logging warning as error message;
    }
}
