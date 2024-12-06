using Easy.Platform.EfCore;
using Easy.Platform.EfCore.Services;
using Easy.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

public class TextSnippetPostgreSqlEfCorePersistenceModule : PlatformEfCorePersistenceModule<TextSnippetDbContext>
{
    public TextSnippetPostgreSqlEfCorePersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    // Override using fulltext search index for BETTER PERFORMANCE
    protected override EfCorePlatformFullTextSearchPersistenceService FullTextSearchPersistenceServiceProvider(IServiceProvider serviceProvider)
    {
        return new TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService(serviceProvider);
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

    protected override Action<DbContextOptionsBuilder> DbContextOptionsBuilderActionProvider(
        IServiceProvider serviceProvider)
    {
        // UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery) for best practice increase performance
        // With(conn => conn.Enlist = false); With(conn => conn.ReadBufferSize = 8192) https://www.npgsql.org/doc/performance.html
        return optionsBuilder => optionsBuilder
            .UseNpgsql(
                new NpgsqlDataSourceBuilder(
                        new NpgsqlConnectionStringBuilder(Configuration.GetConnectionString("PostgreSqlConnection"))
                            .With(conn => conn.Enlist = false)
                            .With(conn => conn.Pooling = true)
                            .With(conn => conn.MinPoolSize = 0) // Always available connection to serve request, reduce latency
                            .With(conn => conn.MaxPoolSize = RecommendedMaxPoolSize) // Setup based on app resource cpu ram max concurrent
                            .With(conn => conn.Timeout = 30)
                            .With(conn => conn.ConnectionIdleLifetime = RecommendedConnectionIdleLifetimeSeconds)
                            .With(conn => conn.ConnectionPruningInterval = RecommendedConnectionIdleLifetimeSeconds)
                            .ToString())
                    .EnableDynamicJson()
                    .Build(),
                opts => opts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                    .CommandTimeout(300)
                    .EnableRetryOnFailure(
                        maxRetryCount: RecommendedConnectionIdleLifetimeSeconds,
                        maxRetryDelay: 1.Seconds(),
                        errorCodesToAdd: null // Specific error codes to retry (null retries common transient errors)
                    ))
            .EnableDetailedErrors();
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

public class TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService : EfCorePlatformFullTextSearchPersistenceService
{
    public TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    // https://www.npgsql.org/efcore/mapping/full-text-search.html#method-2-expression-index
    // builder.HasIndex(p => p.ColName).HasMethod("gin").IsTsVectorExpressionIndex("english");
    public override IQueryable<T> BuildFullTextSearchForSinglePropQueryPart<T>(
        IQueryable<T> originalQuery,
        string fullTextSearchSinglePropName,
        List<string> removedSpecialCharacterSearchTextWords,
        bool exactMatch)
    {
        return originalQuery.Where(
            entity => EF.Functions
                .ToTsVector("english", EF.Property<string>(entity, fullTextSearchSinglePropName))
                .Matches(removedSpecialCharacterSearchTextWords.JoinToString(exactMatch ? " & " : " | ")));
    }

    // Need to execute: CREATE EXTENSION IF NOT EXISTS pg_trgm; => create extension for postgreSQL to support ILike
    // Need to "create index Index_Name on "TableName" using gin("ColumnName" gin_trgm_ops)" <=> builder.HasIndex(p => p.ColName).HasMethod("gin").HasOperators("gin_trgm_ops")
    protected override IQueryable<T> BuildStartWithSearchForSinglePropQueryPart<T>(IQueryable<T> originalQuery, string startWithPropName, string searchText)
    {
        return originalQuery.Where(
            entity => EF.Functions.ILike(EF.Property<string>(entity, startWithPropName), $"{searchText}%"));
    }
}
