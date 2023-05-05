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
            .With(p => p.BadQueryWarning.IsEnabled = true)
            .With(p => p.BadQueryWarning.DefaultTotalItemsThreshold = 100) // Demo warning for getting a lot of data in to memory
            .With(p => p.BadQueryWarning.SlowQueryMillisecondsThreshold = 300)
            .With(p => p.BadQueryWarning.SlowWriteQueryMillisecondsThreshold = 2000)
            .With(p => p.BadQueryWarning.IsLogWarningAsError = true) // Demo logging warning as error message
            .With(
                p => p.BadQueryWarning.CustomTotalItemsThresholds = Util.DictionaryBuilder.New(
                    (typeof(TextSnippetEntity), 10)));
    }
}
