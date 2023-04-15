using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Domain.Entities;

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
            .With(p => p.BadMemoryDataWarning.IsEnabled = true)
            .With(
                p => p.BadMemoryDataWarning.DefaultBadMemoryDataWarningThreshold = 100) // Demo warning for getting a lot of data in to memory
            .With(p => p.BadMemoryDataWarning.IsLogWarningAsError = true) // Demo logging warning as error message
            .With(
                p => p.BadMemoryDataWarning.CustomThresholdBadMemoryDataWarningItems = Util.DictionaryBuilder.New(
                    (typeof(TextSnippetEntity), 10)));
    }
}
