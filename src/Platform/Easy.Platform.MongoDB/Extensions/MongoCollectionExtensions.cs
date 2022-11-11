using System.Linq.Expressions;
using MongoDB.Driver;

namespace Easy.Platform.MongoDB.Extensions;

public static class MongoCollectionExtensions
{
    public static async Task<BulkWriteResult<TItem>> UpsertManyAsync<TItem>(
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

        return await collection.BulkWriteAsync(
            updateRequests,
            new BulkWriteOptions
            {
                IsOrdered = false
            });
    }
}
