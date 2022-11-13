using Easy.Platform.Common.Cqrs;
using Easy.Platform.MongoDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo;

public class TextSnippetDbContext : PlatformMongoDbContext<TextSnippetDbContext>
{
    public TextSnippetDbContext(
        IOptions<PlatformMongoOptions<TextSnippetDbContext>> options,
        IPlatformMongoClient<TextSnippetDbContext> client,
        ILoggerFactory loggerFactory,
        IPlatformCqrs cqrs) : base(options, client, loggerFactory, cqrs)
    {
    }

    public IMongoCollection<TextSnippetEntity> TextSnippetCollection => GetCollection<TextSnippetEntity>();

    public override async Task InternalEnsureIndexesAsync(bool recreate = false)
    {
        if (recreate)
            await Task.WhenAll(
                TextSnippetCollection.Indexes.DropAllAsync());

        await Task.WhenAll(
            TextSnippetCollection.Indexes.CreateManyAsync(
                new List<CreateIndexModel<TextSnippetEntity>>
                {
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.CreatedBy)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.CreatedDate)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.LastUpdatedBy)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.LastUpdatedDate)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.SnippetText)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.Address)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.Addresses)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.AddressStrings)),
                    new(
                        Builders<TextSnippetEntity>.IndexKeys.Text(p => p.SnippetText))
                }));
    }

    public override List<KeyValuePair<Type, string>> EntityTypeToCollectionNameMaps()
    {
        return new List<KeyValuePair<Type, string>>
        {
            new(typeof(TextSnippetEntity), "TextSnippetEntity")
        };
    }
}
