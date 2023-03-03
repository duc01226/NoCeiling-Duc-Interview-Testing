using System.Linq.Expressions;
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
        return options =>
            options.UseNpgsql(Configuration.GetConnectionString("PostgreSqlConnection"));
    }
}

public class TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService : EfCorePlatformFullTextSearchPersistenceService
{
    public TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    // https://www.npgsql.org/efcore/mapping/full-text-search.html#method-2-expression-index
    protected override Expression<Func<T, bool>> BuildFullTextSearchPropsPredicate<T>(
        string searchText,
        List<string> removedSpecialCharacterSearchTextWords,
        List<string> fullTextSearchPropNames,
        bool exactMatch,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchSinglePropPredicatePerWord)
    {
        return removedSpecialCharacterSearchTextWords
            .Select<string, Expression<Func<T, bool>>>(
                searchWord => p => EF.Functions.ToTsVector("english", fullTextSearchPropNames.JoinToString(" ")).Matches(searchWord))
            .Aggregate(
                (currentExpr, nextExpr) => exactMatch ? currentExpr.AndAlso(nextExpr) : currentExpr.Or(nextExpr));
    }

    // Override support search case insensitive for start with by using ILike
    protected override Expression<Func<T, bool>> BuildStartWithSinglePropPredicate<T>(string searchText, string startWithPropName)
    {
        return entity => EF.Functions.ILike(EF.Property<string>(entity, startWithPropName), $"{searchText}%");
    }
}
