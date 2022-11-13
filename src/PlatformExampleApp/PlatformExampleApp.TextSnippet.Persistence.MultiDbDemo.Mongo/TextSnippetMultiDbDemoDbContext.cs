using Easy.Platform.Common.Cqrs;
using Easy.Platform.MongoDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo;

public class TextSnippetMultiDbDemoDbContext : PlatformMongoDbContext<TextSnippetMultiDbDemoDbContext>
{
    public TextSnippetMultiDbDemoDbContext(
        IOptions<PlatformMongoOptions<TextSnippetMultiDbDemoDbContext>> options,
        IPlatformMongoClient<TextSnippetMultiDbDemoDbContext> client,
        ILoggerFactory loggerFactory,
        IPlatformCqrs cqrs) : base(options, client, loggerFactory, cqrs)
    {
    }

    public IMongoCollection<MultiDbDemoEntity> MultiDbDemoEntityCollection => GetCollection<MultiDbDemoEntity>();

    public override async Task InternalEnsureIndexesAsync(bool recreate = false)
    {
        if (recreate)
            await Task.WhenAll(
                MultiDbDemoEntityCollection.Indexes.DropAllAsync());

        await Task.WhenAll(
            MultiDbDemoEntityCollection.Indexes.CreateManyAsync(
                new List<CreateIndexModel<MultiDbDemoEntity>>
                {
                    new CreateIndexModel<MultiDbDemoEntity>(
                        Builders<MultiDbDemoEntity>.IndexKeys.Ascending(p => p.Name))
                }));
    }

    public override List<KeyValuePair<Type, string>> EntityTypeToCollectionNameMaps()
    {
        return new List<KeyValuePair<Type, string>>
        {
            new KeyValuePair<Type, string>(typeof(MultiDbDemoEntity), "MultiDbDemoEntity")
        };
    }
}
