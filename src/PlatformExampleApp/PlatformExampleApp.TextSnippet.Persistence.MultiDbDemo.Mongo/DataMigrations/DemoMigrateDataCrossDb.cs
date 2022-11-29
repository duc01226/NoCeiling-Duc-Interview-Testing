using Easy.Platform.Persistence.DataMigration;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DataMigrations;

internal class DemoMigrateDataCrossDb : PlatformDataMigrationExecutor<TextSnippetMultiDbDemoDbContext>
{
    private readonly DemoMigrateDataCrossDbContext demoMigrateDataCrossDbContext;

    public DemoMigrateDataCrossDb(DemoMigrateDataCrossDbContext demoMigrateDataCrossDbContext)
    {
        this.demoMigrateDataCrossDbContext = demoMigrateDataCrossDbContext;
    }

    public override string Name => GetType().Name;
    public override int Order => 0;

    // Set this data to state that the data migration only valid if db initialized before a certain date
    //public override DateTime? RunOnlyDbInitializedBeforeDate { get; }

    /// <summary>
    /// This application data migration only valid until 2022/12/01
    /// </summary>
    public override DateTime? ExpiredAt => new DateTime(2022, 12, 1);

    public override async Task Execute(TextSnippetMultiDbDemoDbContext dbContext)
    {
        var demoApplicationMigrationEntity = demoMigrateDataCrossDbContext.GetQuery<TextSnippetEntity>()
            .FirstOrDefault(p => p.SnippetText == "DemoMigrateApplicationDataDbContext Entity");
        if (demoApplicationMigrationEntity != null)
        {
            await dbContext.MultiDbDemoEntityCollection.DeleteOneAsync(p => p.Id == demoApplicationMigrationEntity.Id);
            await dbContext.MultiDbDemoEntityCollection.InsertOneAsync(
                new MultiDbDemoEntity
                {
                    Id = demoApplicationMigrationEntity.Id,
                    Name =
                        $"DemoApplicationMigrationEntity.SnippetText: {demoApplicationMigrationEntity.SnippetText}"
                });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            demoMigrateDataCrossDbContext.Dispose();
    }
}
