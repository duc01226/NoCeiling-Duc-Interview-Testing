using Easy.Platform.Application.RequestContext;
using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo;

public sealed class TextSnippetMultiDbDemoDbContext : PlatformMongoDbContext<TextSnippetMultiDbDemoDbContext>
{
    public TextSnippetMultiDbDemoDbContext(
        IOptions<PlatformMongoOptions<TextSnippetMultiDbDemoDbContext>> options,
        IPlatformMongoClient<TextSnippetMultiDbDemoDbContext> client,
        ILoggerFactory loggerFactory,
        IPlatformApplicationRequestContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<TextSnippetMultiDbDemoDbContext> persistenceConfiguration,
        IPlatformRootServiceProvider rootServiceProvider) : base(
        options,
        client,
        loggerFactory,
        userContextAccessor,
        persistenceConfiguration,
        rootServiceProvider)
    {
    }

    public IMongoCollection<MultiDbDemoEntity> MultiDbDemoEntityCollection => GetCollection<MultiDbDemoEntity>();

    public override async Task InternalEnsureIndexesAsync(bool recreate = false)
    {
        if (recreate)
            await Util.TaskRunner.WhenAll(
                MultiDbDemoEntityCollection.Indexes.DropAllAsync());

        await Util.TaskRunner.WhenAll(
            MultiDbDemoEntityCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<MultiDbDemoEntity>(
                    Builders<MultiDbDemoEntity>.IndexKeys.Ascending(p => p.Name))
            ]));
    }

    public override List<KeyValuePair<Type, string>> EntityTypeToCollectionNameMaps()
    {
        return
        [
            new KeyValuePair<Type, string>(typeof(MultiDbDemoEntity), "MultiDbDemoEntity")
        ];
    }
}
