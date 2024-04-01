using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB.Extensions;

public static class MongoCollectionExtensions
{
    public static Task<BulkWriteResult<TItem>> UpsertManyAsync<TItem>(
        this IMongoCollection<TItem> collection,
        List<TItem> items,
        Func<TItem, Expression<Func<TItem, bool>>> updatePredicateBuilder)
    {
        var updateRequests = items
            .Select(
                document => new ReplaceOneModel<TItem>(
                    Builders<TItem>.Filter.Where(updatePredicateBuilder(document)),
                    document)
                {
                    IsUpsert = true
                });

        return collection.BulkWriteAsync(
            updateRequests,
            new BulkWriteOptions
            {
                IsOrdered = false
            });
    }

    public static string? TryToMongoQueryString<TItem>(
        this IEnumerable<TItem> source)
    {
        return source.As<IMongoQueryable<TItem>>()?.ToQueryString();
    }
}
