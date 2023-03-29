using Easy.Platform.EfCore;
using Easy.Platform.EfCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
        return options =>
            options.UseNpgsql(Configuration.GetConnectionString("PostgreSqlConnection"), options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }
}

public class TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService : EfCorePlatformFullTextSearchPersistenceService
{
    public TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    // https://www.npgsql.org/efcore/mapping/full-text-search.html#method-2-expression-index
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

    // For postgres, should use fulltext index for start with support for prefix-search <=> to_tsvector(mycol) @@ to_tsquery('search:*')
    protected override IQueryable<T> BuildStartWithSearchForSinglePropQueryPart<T>(IQueryable<T> originalQuery, string startWithPropName, string searchText)
    {
        return originalQuery.Where(
            entity => EF.Functions
                .ToTsVector("english", EF.Property<string>(entity, startWithPropName))
                .Matches($"{searchText}:*"));
    }
}
