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
    /// Predicate search for single word in search text, default using EF.Functions.Like $"%{searchWord}%".
    /// Override this if you want to modify predicate for search split word by word in search text
    /// Example for SQL : entity => EF.Functions.Contains(EF.Property[string](entity, fullTextSearchPropName), searchWord); SqlServerMigrationUtil.CreateFullTextCatalogIfNotExists(migrationBuilder, $"FTS_EntityName"); SqlServerMigrationUtil.CreateFullTextIndexIfNotExists(columnNames: [fullTextSearchPropName1, fullTextSearchPropName2]) <br />
    /// </summary>
    protected virtual Expression<Func<TEntity, bool>> BuildFullTextSearchSinglePropPerWordPredicate<TEntity>(
        string fullTextSearchPropName,
        string searchWord)
    {
        return entity => EF.Functions.Like(EF.Property<string>(entity, fullTextSearchPropName), $"%{searchWord}%");
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
    public virtual IQueryable<T> BuildSearchQuery<T>(
        IQueryable<T> query,
        string searchText,
        List<string> ignoredSpecialCharactersSearchWords,
        List<string> fullTextSearchPropNames,
        bool exactMatch = false,
        List<string> startWithPropNames = null)
    {
        var fullTextQuery = BuildFullTextSearchQueryPart(query, searchText, ignoredSpecialCharactersSearchWords, fullTextSearchPropNames, exactMatch);
        var startWithQuery = BuildStartWithSearchQueryPart(query, searchText, startWithPropNames);

        // WHY: Should use union instead of OR because UNION is better at performance
        // https://stackoverflow.com/questions/16438556/combining-free-text-search-with-another-condition-is-slow
        return fullTextQuery.PipeIf(startWithQuery != null, p => p.Union(startWithQuery!).Distinct());
    }

    public virtual IQueryable<T> BuildStartWithSearchQueryPart<T>(IQueryable<T> query, string searchText, List<string> startWithPropNames)
    {
        if (startWithPropNames?.Any() != true) return null;

        var predicate = BuildStartWithPropsPredicate<T>(searchText, startWithPropNames);

        return query.Where(predicate!);
    }

    public virtual IQueryable<T> BuildFullTextSearchQueryPart<T>(
        IQueryable<T> query,
        string searchText,
        List<string> ignoredSpecialCharactersSearchWords,
        List<string> fullTextSearchPropNames,
        bool exactMatch = false)
    {
        if (fullTextSearchPropNames.IsEmpty()) return query;

        // WHY: Should use union instead of OR because UNION is better at performance
        // https://stackoverflow.com/questions/16438556/combining-free-text-search-with-another-condition-is-slow
        return fullTextSearchPropNames
            .Select(
                fullTextSearchPropName => BuildFullTextSearchForSinglePropQueryPart(
                    query,
                    fullTextSearchPropName,
                    ignoredSpecialCharactersSearchWords,
                    exactMatch))
            .Aggregate((current, next) => current.Union(next))
            .Distinct();
    }

    public virtual IQueryable<T> BuildFullTextSearchForSinglePropQueryPart<T>(
        IQueryable<T> originalQuery,
        string fullTextSearchSinglePropName,
        List<string> removedSpecialCharacterSearchTextWords,
        bool exactMatch)
    {
        var predicate = removedSpecialCharacterSearchTextWords
            .Select(searchWord => BuildFullTextSearchSinglePropPerWordPredicate<T>(fullTextSearchSinglePropName, searchWord))
            .Aggregate((current, next) => exactMatch ? current.AndAlso(next) : current.Or(next));

        return originalQuery.Where(predicate);
    }

    protected override IQueryable<T> DoSearch<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextExactMatch = false,
        Expression<Func<T, object>>[] includeStartWithProps = null)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        var ignoredSpecialCharactersSearchWords = BuildIgnoredSpecialCharactersSearchWords(searchText.Trim());
        var fullTextSearchPropNames =
            inFullTextSearchProps.Where(p => p != null).Select(ExpressionExtension.GetPropertyName).ToList();
        var includeStartWithPropNames =
            includeStartWithProps?.Where(p => p != null).Select(ExpressionExtension.GetPropertyName).ToList();

        var searchedQuery = BuildSearchQuery(
            query,
            searchText,
            ignoredSpecialCharactersSearchWords,
            fullTextSearchPropNames,
            fullTextExactMatch,
            includeStartWithPropNames);

        return searchedQuery;
    }

    public virtual List<string> BuildIgnoredSpecialCharactersSearchWords(string searchText)
    {
        var specialCharacters = new[] { '\\', '~', '[', ']', '(', ')', '!' };

        // Remove special not supported character for full text search
        var removedSpecialCharactersSearchText = specialCharacters.Aggregate(searchText, (current, next) => current.Replace(next.ToString(), " "));

        var searchWords = removedSpecialCharactersSearchText.Split(" ")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return searchWords;
    }

    /// <summary>
    /// BuildStartWithPropsPredicate default.
    /// Example: Search text "abc def". Expression: .Or(EF.Functions.Like('%abc%')).Or(EF.Functions.Like('%def%'))
    /// </summary>
    protected virtual Expression<Func<T, bool>> BuildStartWithPropsPredicate<T>(
        string searchText,
        List<string> startWithPropNames)
    {
        return startWithPropNames
            .Select(startWithPropName => BuildStartWithSinglePropPredicate<T>(searchText, startWithPropName))
            .Aggregate((resultPredicate, nextPredicate) => resultPredicate.Or(nextPredicate));
    }

    /// <summary>
    /// BuildStartWithSinglePropPredicate default.
    /// Example: EF.Functions.Like('%abc%')
    /// </summary>
    protected virtual Expression<Func<T, bool>> BuildStartWithSinglePropPredicate<T>(
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

    protected override Expression<Func<TEntity, bool>> BuildFullTextSearchSinglePropPerWordPredicate<TEntity>(string fullTextSearchPropName, string searchWord)
    {
        return entity => EF.Functions.Like(EF.Property<string>(entity, fullTextSearchPropName), $"%{searchWord}%");
    }
}
