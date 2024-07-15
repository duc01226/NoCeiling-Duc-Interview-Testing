using Easy.Platform.Application;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Application.Persistence;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo;

public sealed class TextSnippetDbContext : PlatformMongoDbContext<TextSnippetDbContext>, ITextSnippetDbContext
{
    public TextSnippetDbContext(
        IPlatformMongoDatabase<TextSnippetDbContext> database,
        ILoggerFactory loggerFactory,
        IPlatformApplicationRequestContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<TextSnippetDbContext> persistenceConfiguration,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        database,
        loggerFactory,
        userContextAccessor,
        persistenceConfiguration,
        rootServiceProvider,
        applicationSettingContext)
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
            [
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.CreatedBy)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.CreatedDate)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.LastUpdatedBy)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.LastUpdatedDate)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.SnippetText)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.Address)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.Addresses)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys.Ascending(p => p.AddressStrings)),
                new CreateIndexModel<TextSnippetEntity>(
                    Builders<TextSnippetEntity>.IndexKeys
                        .Text(p => p.SnippetText)
                        .Text(p => p.FullText))
            ]));
    }

    public override List<KeyValuePair<Type, string>> EntityTypeToCollectionNameMaps()
    {
        return
        [
            new KeyValuePair<Type, string>(typeof(TextSnippetEntity), "TextSnippetEntity")
        ];
    }
}
