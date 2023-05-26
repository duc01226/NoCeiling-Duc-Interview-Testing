using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

public sealed class DemoMigrateDataCrossDbContext : PlatformMongoDbContext<DemoMigrateDataCrossDbContext>
{
    public DemoMigrateDataCrossDbContext(
        IOptions<PlatformMongoOptions<DemoMigrateDataCrossDbContext>> options,
        IPlatformMongoClient<DemoMigrateDataCrossDbContext> client,
        ILoggerFactory loggerFactory,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<DemoMigrateDataCrossDbContext> persistenceConfiguration) : base(
        options,
        client,
        loggerFactory,
        userContextAccessor,
        persistenceConfiguration)
    {
    }

    public IMongoCollection<TextSnippetEntity> TextSnippetEntityCollection => GetCollection<TextSnippetEntity>();

    public override async Task InternalEnsureIndexesAsync(bool recreate = false)
    {
        await Task.CompletedTask;
    }

    public override async Task Initialize(IServiceProvider serviceProvider)
    {
        // Insert fake data before run DemoMigrateApplicationDataCrossDb
        if (!TextSnippetEntityCollection.AsQueryable()
            .Any(p => p.SnippetText == "DemoMigrateApplicationDataDbContext Entity"))
            await TextSnippetEntityCollection.InsertOneAsync(
                TextSnippetEntity.Create(
                    id: Guid.NewGuid(),
                    snippetText: "DemoMigrateApplicationDataDbContext Entity",
                    fullText: "DemoMigrateApplicationDataDbContext Entity"));

        await base.Initialize(serviceProvider);
    }
}
