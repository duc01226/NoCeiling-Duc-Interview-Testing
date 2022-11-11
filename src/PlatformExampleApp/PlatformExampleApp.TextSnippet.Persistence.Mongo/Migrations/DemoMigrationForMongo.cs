using Easy.Platform.MongoDB.Migration;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo.Migrations;

internal class DemoMigrationForMongo : PlatformMongoMigrationExecutor<TextSnippetDbContext>
{
    public override string Name => "20220901000000_DemoMigrationForMongo";

    /// <summary>
    /// Demo set the expired date if the migration should be execute one time. After deploying it could be deleted.
    /// </summary>
    public override DateTime? ExpiredDate => new DateTime(2022, 1, 1);

    public override async Task Execute(TextSnippetDbContext dbContext)
    {
        await dbContext.EnsureIndexesAsync(true);
    }
}
