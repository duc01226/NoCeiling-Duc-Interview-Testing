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

    public override string Name => "20500101000000_DemoMigrateDataCrossDb";
    public override DateTime CreationDate => new DateTime(2050, 01, 01);

    /// <summary>
    /// This application data migration only valid until 2022/12/01
    /// </summary>
    public override DateTime? ExpirationDate => new DateTime(2050, 01, 01);

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
