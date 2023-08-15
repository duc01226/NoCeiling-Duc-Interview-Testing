using System.Linq.Expressions;
using Easy.Platform.Persistence.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB.Services;

public class MongoDbPlatformFullTextSearchPersistenceService : PlatformFullTextSearchPersistenceService
{
    public MongoDbPlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override bool IsSupportQuery<T>(IQueryable<T> query)
    {
        return query is IMongoQueryable;
    }

    protected override IQueryable<T> DoSearch<T>(
        IQueryable<T> query,
        string searchText,
        Expression<Func<T, object>>[] inFullTextSearchProps,
        bool fullTextAccurateMatch = true,
        Expression<Func<T, object>>[] includeStartWithProps = null)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        var textSearchMongoFilter = BuildTextSearchMongoFilterDefinition<T>(searchText, fullTextAccurateMatch);
        var startsWithFilter = includeStartWithProps?.Any() == true
            ? BuildStartsWithMongoFilterDefinition(searchText, includeStartWithProps)
            : null;

        var finalFilter = textSearchMongoFilter
            .PipeIf(startsWithFilter != null, p => Builders<T>.Filter.Or(p, startsWithFilter));

        return ((IMongoQueryable<T>)query).Where(_ => finalFilter.Inject());
    }

    public static FilterDefinition<T> BuildTextSearchMongoFilterDefinition<T>(
        string searchText,
        bool fullTextAccurateMatch)
    {
        return Builders<T>.Filter.Text(
            fullTextAccurateMatch ? $"{searchText.Trim().Split(' ').Select(word => $"\"{word}\"").JoinToString(" ")}" : searchText.Trim(),
            new TextSearchOptions
            {
                CaseSensitive = false,
                DiacriticSensitive = false
            });
    }

    public static FilterDefinition<T> BuildStartsWithMongoFilterDefinition<T>(
        string searchText,
        Expression<Func<T, object>>[] includeStartWithProps)
    {
        var filterBuilder = Builders<T>.Filter;
        var startsWithFilters = includeStartWithProps
            .Select(p => filterBuilder.Regex(p, new BsonRegularExpression($"^({searchText})", "i")))
            .ToList();
        return filterBuilder.Or(startsWithFilters);
    }
}
