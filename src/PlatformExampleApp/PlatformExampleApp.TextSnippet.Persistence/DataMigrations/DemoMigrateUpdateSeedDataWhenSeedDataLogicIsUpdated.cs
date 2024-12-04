using Easy.Platform.Persistence.DataMigration;
using PlatformExampleApp.TextSnippet.Application.DataSeeders;

namespace PlatformExampleApp.TextSnippet.Persistence.DataMigrations;

internal sealed class DemoMigrateUpdateSeedDataWhenSeedDataLogicIsUpdated : PlatformDataMigrationExecutor<TextSnippetDbContext>
{
    private readonly DemoSeedDataUseCommandSolutionDataSeeder demoSeedDataUseCommandSolutionDataSeeder;

    public DemoMigrateUpdateSeedDataWhenSeedDataLogicIsUpdated(
        IPlatformRootServiceProvider rootServiceProvider,
        DemoSeedDataUseCommandSolutionDataSeeder demoSeedDataUseCommandSolutionDataSeeder) : base(rootServiceProvider)
    {
        this.demoSeedDataUseCommandSolutionDataSeeder = demoSeedDataUseCommandSolutionDataSeeder;
    }

    public override string Name => "20220130_DemoMigrateUpdateSeedDataWhenSeedDataLogicIsUpdated";
    public override DateTime? OnlyForDbsCreatedBeforeDate => new(2022, 01, 30);

    // Demo can override this to allow DataMigration execution parallel in background thread, allow not wait, do not block the application start
    public override bool AllowRunInBackgroundThread => true;

    public override async Task Execute(TextSnippetDbContext dbContext)
    {
        await demoSeedDataUseCommandSolutionDataSeeder.SeedData(isReplaceNewSeedData: true);
    }
}
