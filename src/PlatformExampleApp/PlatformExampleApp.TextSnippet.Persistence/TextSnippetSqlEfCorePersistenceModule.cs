using System.Linq.Expressions;
using Easy.Platform.EfCore;
using Easy.Platform.EfCore.Services;
using Easy.Platform.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

    public override PlatformPersistenceConfigurationPooledDbContextOptions PooledDbContextOption()
    {
        return new PlatformPersistenceConfigurationPooledDbContextOptions
        {
            Enabled = true,
            PoolSize = 1000
        };
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
        // With(conn => conn.Enlist = false);With(conn => conn.ReadBufferSize = 8192) https://www.npgsql.org/doc/performance.html

        // (I) LoadBalanceTimeout => To configure a DbContext to release its SQL connection shortly after being idle (e.g., within 3 seconds)
        // => prevent max connection pool error, no connection if a db-context is idling (example run paging for a long time but has opened a db context outside and wait)
        return options => options
            .UseSqlServer(
                new SqlConnectionStringBuilder(Configuration.GetConnectionString("DefaultConnection"))
                    .With(conn => conn.Enlist = false)
                    .With(conn => conn.LoadBalanceTimeout = RecommendedConnectionIdleLifetimeSeconds) // (I)
                    .With(conn => conn.Pooling = true)
                    .With(conn => conn.MinPoolSize = 0) // Always available connection to serve request, reduce latency
                    .With(conn => conn.MaxPoolSize = RecommendedMaxPoolSize) // Setup based on app resource cpu ram max concurrent
                    .ToString(),
                options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                    .EnableRetryOnFailure(
                        maxRetryCount: RecommendedConnectionRetryOnFailureCount,
                        maxRetryDelay: RecommendedConnectionRetryDelay,
                        errorNumbersToAdd: null // Specific error codes to retry (null retries common transient errors)
                    ))
            .EnableDetailedErrors();
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
