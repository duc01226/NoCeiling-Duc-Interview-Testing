using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Persistence.Services;

public interface IPlatformFullTextSearchPersistenceService : IPersistenceService
{
    /// <summary>
    /// Filter by search text, support multiple string prop. inFullTextSearchProps must be a list of one level string prop.
    /// Ex: Input searchText: "abc def", inFullTextSearchProps: [p => p.PropA, p => p.PropB] will return query which is (p.PropA contains ("abc" AND "def") OR p.PropB contains ("abc" AND "def")).
    /// </summary>
    /// <typeparam name="T">Query item Type.</typeparam>
    /// <param name="query">Query to search on.</param>
    /// <param name="searchText">Search text.</param>
    /// <param name="inFullTextSearchProps">List of property expression to search by full-text on.</param>
    /// <param name="fullTextExactMatch">Whether search fulltext matching exact phrase or not</param>
    /// <param name="includeStartWithProps">List of property expression to search by startWith on.</param>
    /// <returns>Filtered by search text query.</returns>
    public IQueryable<T> Search<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextExactMatch = false,
        Expression<Func<T, object>>[] includeStartWithProps = null) where T : class;

    public bool IsSupportQuery<T>(IQueryable<T> query) where T : class;
}

public abstract class PlatformFullTextSearchPersistenceService : IPlatformFullTextSearchPersistenceService
{
    protected readonly IServiceProvider ServiceProvider;

    public PlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public abstract IQueryable<T> Search<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextExactMatch = false,
        Expression<Func<T, object>>[] includeStartWithProps = null) where T : class;

    public abstract bool IsSupportQuery<T>(IQueryable<T> query) where T : class;

    protected bool TrySearchByFirstSupportQueryHelper<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool exactMatch,
        out IQueryable<T> newQuery,
        Expression<Func<T, object>>[] includeStartWithProps = null) where T : class
    {
        var otherSupportHelper = ServiceProvider?
            .GetServices<IPlatformFullTextSearchPersistenceService>()
            .FirstOrDefault(p => p.IsSupportQuery(query));

        if (otherSupportHelper != null)
        {
            newQuery = otherSupportHelper.Search(
                query,
                searchText,
                inFullTextSearchProps,
                exactMatch,
                includeStartWithProps);
            return true;
        }

        newQuery = query;
        return false;
    }
}
