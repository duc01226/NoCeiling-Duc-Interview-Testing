using Easy.Platform.EfCore;
using Easy.Platform.EfCore.Services;
using Easy.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Domain.Entities;

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
        return options => options
            .UseNpgsql(
                Configuration.GetConnectionString("PostgreSqlConnection"),
                options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseLazyLoadingProxies();
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
