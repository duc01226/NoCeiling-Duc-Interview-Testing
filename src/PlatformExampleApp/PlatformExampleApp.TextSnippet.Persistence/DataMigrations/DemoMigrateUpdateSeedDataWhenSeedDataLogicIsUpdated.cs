using Easy.Platform.Persistence.DataMigration;
using PlatformExampleApp.TextSnippet.Application.DataSeeders;

namespace PlatformExampleApp.TextSnippet.Persistence.DataMigrations;

internal class DemoMigrateUpdateSeedDataWhenSeedDataLogicIsUpdated : PlatformDataMigrationExecutor<TextSnippetDbContext>
{
    private readonly DemoSeedDataUseCommandSolutionDataSeeder demoSeedDataUseCommandSolutionDataSeeder;

    public DemoMigrateUpdateSeedDataWhenSeedDataLogicIsUpdated(DemoSeedDataUseCommandSolutionDataSeeder demoSeedDataUseCommandSolutionDataSeeder)
    {
        this.demoSeedDataUseCommandSolutionDataSeeder = demoSeedDataUseCommandSolutionDataSeeder;
    }

    public override string Name => "20220130_DemoMigrateUpdateSeedDataWhenSeedDataLogicIsUpdated";
    public override DateTime CreationDate => new(2022, 01, 30);

    public override async Task Execute(TextSnippetDbContext dbContext)
    {
        await demoSeedDataUseCommandSolutionDataSeeder.SeedData(isReplaceNewSeedData: true);
    }
}
