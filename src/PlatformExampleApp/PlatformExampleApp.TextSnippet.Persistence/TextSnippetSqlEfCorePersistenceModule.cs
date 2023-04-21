using System.Linq.Expressions;
using Easy.Platform.EfCore;
using Easy.Platform.EfCore.Services;
using Easy.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence;

public class TextSnippetSqlEfCorePersistenceModule : PlatformEfCorePersistenceModule<TextSnippetDbContext>
{
    public TextSnippetSqlEfCorePersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    // Override using fulltext search index for BETTER PERFORMANCE
    protected override EfCorePlatformFullTextSearchPersistenceService FullTextSearchPersistenceServiceProvider(IServiceProvider serviceProvider)
    {
        return new TextSnippetSqlEfCorePlatformFullTextSearchPersistenceService(serviceProvider);
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
            .With(p => p.BadQueryWarning.SlowQueryMillisecondsThreshold = 1000)
            .With(p => p.BadQueryWarning.IsLogWarningAsError = true) // Demo logging warning as error message
            .With(
                p => p.BadQueryWarning.CustomTotalItemsThresholds = Util.DictionaryBuilder.New(
                    (typeof(TextSnippetEntity), 10)));
    }

    // This example config help to override to config outbox config
    //protected override PlatformOutboxConfig OutboxConfigProvider(IServiceProvider serviceProvider)
    //{
    //    var defaultConfig = new PlatformOutboxConfig
    //    {
    //        // You may only want to set this to true only when you are using mix old system and new platform code. You do not call uow.complete
    //        // after call sendMessages. This will force sending message always start use there own uow
    //        ForceAlwaysSendOutboxInNewUow = true
    //    };

    //    return defaultConfig;
    //}

    protected override Action<DbContextOptionsBuilder> DbContextOptionsBuilderActionProvider(
        IServiceProvider serviceProvider)
    {
        // UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery) for best practice increase performance
        return options =>
            options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"), options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }
}

public class TextSnippetSqlEfCorePlatformFullTextSearchPersistenceService : EfCorePlatformFullTextSearchPersistenceService
{
    public TextSnippetSqlEfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected override Expression<Func<TEntity, bool>> BuildFullTextSearchSinglePropPerWordPredicate<TEntity>(string fullTextSearchPropName, string searchWord)
    {
        return entity => EF.Functions.Contains(EF.Property<string>(entity, fullTextSearchPropName), searchWord);
    }
}
