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
    protected virtual Expression<Func<TEntity, bool>> BuildFullTextSearchSinglePropPredicatePerWord<TEntity>(
        string fullTextSearchPropName,
        string searchWord)
    {
        return entity => EF.Functions.Like(EF.Property<string>(entity, fullTextSearchPropName), $"%{searchWord}%");
    }

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
            (fullTextSearchPropName, searchWord) => BuildFullTextSearchSinglePropPredicatePerWord<T>(fullTextSearchPropName, searchWord),
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
        List<string> removedSpecialCharacterSearchTextWords,
        List<string> fullTextSearchPropNames,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchSinglePropPredicatePerWord,
        bool exactMatch = false,
        List<string> startWithPropNames = null)
    {
        var fullTextSearchPropsPredicate = BuildFullTextSearchPropsPredicate(
            searchText,
            removedSpecialCharacterSearchTextWords,
            fullTextSearchPropNames,
            exactMatch,
            buildFullTextSearchSinglePropPredicatePerWord);
        var startWithPropsPredicate = startWithPropNames?.Any() == true
            ? BuildStartWithPropsPredicate<T>(searchText, startWithPropNames)
            : null;

        // WHY: Should use union instead of OR because UNION is better at performance
        // https://stackoverflow.com/questions/16438556/combining-free-text-search-with-another-condition-is-slow
        return query.Where(fullTextSearchPropsPredicate)
            .PipeIf(startWithPropsPredicate != null, p => p.Union(query.Where(startWithPropsPredicate!)));
    }

    public IQueryable<T> DoSearch<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextExactMatch,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchSinglePropPredicatePerWord,
        Expression<Func<T, object>>[] includeStartWithProps = null)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        var searchWords = IPlatformFullTextSearchPersistenceService.BuildSearchWordsIgnoreSpecialCharacters(searchText.Trim());
        var fullTextSearchPropNames =
            inFullTextSearchProps.Where(p => p != null).Select(ExpressionExtension.GetPropertyName).ToList();
        var includeStartWithPropNames =
            includeStartWithProps?.Where(p => p != null).Select(ExpressionExtension.GetPropertyName).ToList();

        var searchedQuery = BuildSearchQuery(
            query,
            searchText,
            searchWords,
            fullTextSearchPropNames,
            buildFullTextSearchSinglePropPredicatePerWord,
            fullTextExactMatch,
            includeStartWithPropNames);

        return searchedQuery;
    }

    /// <summary>
    /// BuildFullTextSearchPropsPredicate default.
    /// Example: Search text "abc def". Expression: .Or(EF.Functions.Like('%abc%')).Or(EF.Functions.Like('%def%'))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="searchText"></param>
    /// <param name="removedSpecialCharacterSearchTextWords"></param>
    /// <param name="fullTextSearchPropNames"></param>
    /// <param name="exactMatch"></param>
    /// <param name="buildFullTextSearchSinglePropPredicatePerWord">(string fullTextSearchPropName, string searchWord) => Expression<Func<TEntity, bool>> BuildFullTextSearchPropPredicate<TEntity></param>
    /// <returns></returns>
    protected virtual Expression<Func<T, bool>> BuildFullTextSearchPropsPredicate<T>(
        string searchText,
        List<string> removedSpecialCharacterSearchTextWords,
        List<string> fullTextSearchPropNames,
        bool exactMatch,
        Func<string, string, Expression<Func<T, bool>>> buildFullTextSearchSinglePropPredicatePerWord)
    {
        var fullTextSearchPropsPredicate = fullTextSearchPropNames
            .Select(
                fullTextSearchPropName =>
                {
                    return removedSpecialCharacterSearchTextWords
                        .Select(searchWord => buildFullTextSearchSinglePropPredicatePerWord(fullTextSearchPropName, searchWord))
                        .Aggregate(
                            (resultPredicate, nextPredicate) =>
                                exactMatch
                                    ? resultPredicate.AndAlso(nextPredicate)
                                    : resultPredicate.Or(nextPredicate));
                })
            .Aggregate((resultPredicate, nextSinglePropPredicate) => resultPredicate.Or(nextSinglePropPredicate));
        return fullTextSearchPropsPredicate;
    }

    protected virtual Expression<Func<T, bool>> BuildStartWithPropsPredicate<T>(
        string searchText,
        List<string> startWithPropNames)
    {
        var startWithPropsPredicate = startWithPropNames
            .Select(startWithPropName => BuildStartWithSinglePropPredicate<T>(searchText, startWithPropName))
            .Aggregate((resultPredicate, nextPredicate) => resultPredicate.Or(nextPredicate));
        return startWithPropsPredicate;
    }

    /// <summary>
    /// Default use EF.Functions.Like for startWith
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

    protected override Expression<Func<TEntity, bool>> BuildFullTextSearchSinglePropPredicatePerWord<TEntity>(string fullTextSearchPropName, string searchWord)
    {
        return entity => EF.Functions.Like(EF.Property<string>(entity, fullTextSearchPropName), $"%{searchWord}%");
    }
}
