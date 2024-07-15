using Easy.Platform.Application;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.MongoDB;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

public sealed class DemoMigrateDataCrossDbContext : PlatformMongoDbContext<DemoMigrateDataCrossDbContext>
{
    public DemoMigrateDataCrossDbContext(
        IPlatformMongoDatabase<DemoMigrateDataCrossDbContext> database,
        ILoggerFactory loggerFactory,
        IPlatformApplicationRequestContextAccessor userContextAccessor,
        PlatformPersistenceConfiguration<DemoMigrateDataCrossDbContext> persistenceConfiguration,
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
                    id: Ulid.NewUlid().ToString(),
                    snippetText: "DemoMigrateApplicationDataDbContext Entity",
                    fullText: "DemoMigrateApplicationDataDbContext Entity"));

        await base.Initialize(serviceProvider);
    }
}
