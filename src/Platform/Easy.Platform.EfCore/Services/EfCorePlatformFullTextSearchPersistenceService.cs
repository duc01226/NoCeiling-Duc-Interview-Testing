using System.Linq.Expressions;
using Easy.Platform.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Easy.Platform.EfCore.Services;

public abstract class EfCorePlatformFullTextSearchPersistenceService : PlatformFullTextSearchPersistenceService
{
    public EfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <summary>
    /// Example for SQL : entity => EF.Functions.Contains(EF.Property[string](entity, fullTextSearchPropName), searchWord); SqlServerMigrationUtil.CreateFullTextCatalogIfNotExists(migrationBuilder, $"FTS_EntityName"); SqlServerMigrationUtil.CreateFullTextIndexIfNotExists(columnNames: [fullTextSearchPropName1, fullTextSearchPropName2]) <br/>
    /// Example for Postgres : entity => EF.Functions.ToTsVector(EF.Property[string](entity, fullTextSearchPropName)).Matches(searchWord); builder.HasIndex(p => new { p.fullTextSearchPropName1, p.fullTextSearchPropName2 }).HasMethod("GIN").IsTsVectorExpressionIndex("english") <br/>
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="fullTextSearchPropName"></param>
    /// <param name="searchWord"></param>
    /// <returns></returns>
    protected abstract Expression<Func<TEntity, bool>> BuildFullTextSearchPropPredicate<TEntity>(string fullTextSearchPropName, string searchWord);

    public override IQueryable<T> Search<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextExactMatch = false,
        Expression<Func<T, object>>[] includeStartWithProps = null)
    {
        if (!IsSupportQuery(query) &&
            TrySearchByFirstSupportQueryHelper(
                query,
                searchText,
                inFullTextSearchProps,
                fullTextExactMatch,
                out var newQuery,
                includeStartWithProps))
            return newQuery;

        return DoSearch(
            query,
            searchText,
            inFullTextSearchProps,
            fullTextExactMatch,
            (fullTextSearchPropName, searchWord) => BuildFullTextSearchPropPredicate<T>(fullTextSearchPropName, searchWord),
            includeStartWithProps);
    }

    public override bool IsSupportQuery<T>(IQueryable<T> query) where T : class
    {
        var queryType = query.GetType();
        return queryType.IsAssignableTo(typeof(DbSet<T>)) ||
               queryType.IsAssignableTo(typeof(IInfrastructure<T>)) ||
               queryType.IsAssignableTo(typeof(EntityQueryable<T>));
    }

    /// <summary>
    /// Build query for all search prop. Example: Search by PropA, PropB for text "hello word" will generate query with predicate:
    /// (propA.Contains("hello") AND propA.Contains("word")) OR (propB.Contains("hello") AND propB.Contains("word")).
    /// </summary>
    public IQueryable<T> BuildSearchQuery<T>(
        IQueryable<T> query,
        string searchText,
        List<string> searchWords,
        List<string> fullTextSearchPropNames,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchPropPredicate,
        bool exactMatch = false,
        List<string> startWithPropNames = null)
    {
        var fullTextSearchPropsPredicate = BuildFullTextSearchPropsPredicate(
            searchWords,
            fullTextSearchPropNames,
            exactMatch,
            buildFullTextSearchPropPredicate);
        var startWithPropsPredicate = startWithPropNames?.Any() == true
            ? BuildStartWithPropsPredicate<T>(searchText, startWithPropNames)
            : null;

        // WHY: Should use union instead of OR because UNION is better at performance
        // https://stackoverflow.com/questions/16438556/combining-free-text-search-with-another-condition-is-slow
        return query.Where(fullTextSearchPropsPredicate)
            .PipeIf(startWithPropsPredicate != null, p => p.Union(query.Where(startWithPropsPredicate!)));
    }

    public static List<string> BuildSearchWords(string searchText)
    {
        // Remove special not supported character for full text search
        var removedSpecialCharactersSearchText = searchText
            .Replace("\"", " ")
            .Replace("~", " ")
            .Replace("[", " ")
            .Replace("]", " ")
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace("!", " ");

        var searchWords = removedSpecialCharactersSearchText.Split(" ")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return searchWords;
    }

    public IQueryable<T> DoSearch<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextExactMatch,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchPropPredicate,
        Expression<Func<T, object>>[] includeStartWithProps = null)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        var searchWords = BuildSearchWords(searchText.Trim());
        var fullTextSearchPropNames =
            inFullTextSearchProps.Where(p => p != null).Select(ExpressionExtension.GetPropertyName).ToList();
        var includeStartWithPropNames =
            includeStartWithProps?.Where(p => p != null).Select(ExpressionExtension.GetPropertyName).ToList();

        var searchedQuery = BuildSearchQuery(
            query,
            searchText,
            searchWords,
            fullTextSearchPropNames,
            buildFullTextSearchPropPredicate,
            fullTextExactMatch,
            includeStartWithPropNames);

        return searchedQuery;
    }

    /// <summary>
    /// BuildFullTextSearchPropsPredicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="searchWords"></param>
    /// <param name="fullTextSearchPropNames"></param>
    /// <param name="exactMatch"></param>
    /// <param name="buildFullTextSearchPropPredicate">(string fullTextSearchPropName, string searchWord) => Expression<Func<TEntity, bool>> BuildFullTextSearchPropPredicate<TEntity></param>
    /// <returns></returns>
    protected Expression<Func<T, bool>> BuildFullTextSearchPropsPredicate<T>(
        List<string> searchWords,
        List<string> fullTextSearchPropNames,
        bool exactMatch,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchPropPredicate)
    {
        var fullTextSearchPropsPredicate = fullTextSearchPropNames
            .Select(
                fullTextSearchPropName =>
                {
                    return searchWords
                        .Select(searchWord => buildFullTextSearchPropPredicate(fullTextSearchPropName, searchWord))
                        .Aggregate(
                            (resultPredicate, nextPredicate) =>
                                exactMatch
                                    ? resultPredicate.AndAlso(nextPredicate)
                                    : resultPredicate.Or(nextPredicate));
                })
            .Aggregate((resultPredicate, nextSinglePropPredicate) => resultPredicate.Or(nextSinglePropPredicate));
        return fullTextSearchPropsPredicate;
    }

    protected Expression<Func<T, bool>> BuildStartWithPropsPredicate<T>(
        string searchText,
        List<string> startWithPropNames)
    {
        var startWithPropsPredicate = startWithPropNames
            .Select(startWithPropName => BuildStartWithPropPredicate<T>(searchText, startWithPropName))
            .Aggregate((resultPredicate, nextPredicate) => resultPredicate.Or(nextPredicate));
        return startWithPropsPredicate;
    }

    protected virtual Expression<Func<T, bool>> BuildStartWithPropPredicate<T>(
        string searchText,
        string startWithPropName)
    {
        return entity => EF.Functions.Like(EF.Property<string>(entity, startWithPropName), $"{searchText}%");
    }
}

/// <summary>
/// This will use Like Operation for fulltext search
/// </summary>
public class LikeOperationEfCorePlatformFullTextSearchPersistenceService : EfCorePlatformFullTextSearchPersistenceService
{
    public LikeOperationEfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected override Expression<Func<TEntity, bool>> BuildFullTextSearchPropPredicate<TEntity>(string fullTextSearchPropName, string searchWord)
    {
        return entity => EF.Functions.Like(EF.Property<string>(entity, fullTextSearchPropName), $"%{searchWord}%");
    }
}
