using Easy.Platform.Persistence.DataMigration;
using MongoDB.Driver;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DemoMigrateDataCrossDb;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.DataMigrations;

internal sealed class DemoMigrateDataCrossDb : PlatformDataMigrationExecutor<TextSnippetMultiDbDemoDbContext>
{
    private readonly DemoMigrateDataCrossDbContext demoMigrateDataCrossDbContext;

    public DemoMigrateDataCrossDb(IPlatformRootServiceProvider rootServiceProvider, DemoMigrateDataCrossDbContext demoMigrateDataCrossDbContext) : base(
        rootServiceProvider)
    {
        this.demoMigrateDataCrossDbContext = demoMigrateDataCrossDbContext;
    }

    public override string Name => "20500101000000_DemoMigrateDataCrossDb";
    public override DateTime CreationDate => new(2050, 01, 01);

    /// <summary>
    /// This application data migration only valid until 2022/12/01
    /// </summary>
    public override DateTime? ExpirationDate => new DateTime(2050, 01, 01);

    // Demo can override this to allow DataMigration execution parallel in background thread, allow not wait, do not block the application start
    public override bool AllowRunInBackgroundThread => true;

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
