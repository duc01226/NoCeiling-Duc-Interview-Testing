using Easy.Platform.MongoDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

public class DemoMigrateDataCrossDbContext : PlatformMongoDbContext<DemoMigrateDataCrossDbContext>
{
    public DemoMigrateDataCrossDbContext(
        IOptions<PlatformMongoOptions<DemoMigrateDataCrossDbContext>> options,
        IPlatformMongoClient<DemoMigrateDataCrossDbContext> client,
        ILoggerFactory loggerFactory) : base(options, client, loggerFactory)
    {
    }

    public IMongoCollection<TextSnippetEntity> TextSnippetEntityCollection => GetCollection<TextSnippetEntity>();

    public override Task InternalEnsureIndexesAsync(bool recreate = false)
    {
        return Task.CompletedTask;
    }

    public override async Task Initialize(IServiceProvider serviceProvider)
    {
        // Insert fake data before run DemoMigrateApplicationDataCrossDb
        if (!TextSnippetEntityCollection.AsQueryable()
            .Any(p => p.SnippetText == "DemoMigrateApplicationDataDbContext Entity"))
            await TextSnippetEntityCollection.InsertOneAsync(
                new TextSnippetEntity
                {
                    Id = Guid.NewGuid(),
                    SnippetText = "DemoMigrateApplicationDataDbContext Entity",
                    FullText = "DemoMigrateApplicationDataDbContext Entity"
                });

        await base.Initialize(serviceProvider);
    }
}
