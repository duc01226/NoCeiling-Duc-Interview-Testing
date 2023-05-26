using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Utils;
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
        IPlatformApplicationUserContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<TextSnippetMultiDbDemoDbContext> persistenceConfiguration) : base(
        options,
        client,
        loggerFactory,
        userContextAccessor,
        persistenceConfiguration)
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
                new List<CreateIndexModel<MultiDbDemoEntity>>
                {
                    new(
                        Builders<MultiDbDemoEntity>.IndexKeys.Ascending(p => p.Name))
                }));
    }

    public override List<KeyValuePair<Type, string>> EntityTypeToCollectionNameMaps()
    {
        return new List<KeyValuePair<Type, string>>
        {
            new(typeof(MultiDbDemoEntity), "MultiDbDemoEntity")
        };
    }
}
