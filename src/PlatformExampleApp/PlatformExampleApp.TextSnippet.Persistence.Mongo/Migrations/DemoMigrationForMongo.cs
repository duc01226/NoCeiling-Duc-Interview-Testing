using Easy.Platform.MongoDB.Migration;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo.Migrations;

internal sealed class DemoMigrationForMongo : PlatformMongoMigrationExecutor<TextSnippetDbContext>
{
    public override string Name => "20230901000000_DemoMigrationForMongo";
    public override DateTime? OnlyForDbInitBeforeDate => new DateTime(2023, 09, 01);

    /// <summary>
    /// Demo set the expired date if the migration should be execute one time. After deploying it could be deleted.
    /// </summary>
    public override DateTime? ExpirationDate => new DateTime(2025, 1, 1);

    public override async Task Execute(TextSnippetDbContext dbContext)
    {
        await dbContext.EnsureIndexesAsync(true);
    }
}
