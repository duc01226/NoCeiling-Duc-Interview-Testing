using Easy.Platform.Application;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.DataSeeders;

public class DummyPerformanceTestTextSnippetApplicationDataSeeder : PlatformApplicationDataSeeder
{
    private readonly ITextSnippetRootRepository<TextSnippetEntity> textSnippetRepository;

    public DummyPerformanceTestTextSnippetApplicationDataSeeder(
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ITextSnippetRootRepository<TextSnippetEntity> textSnippetRepository) : base(unitOfWorkManager, serviceProvider, configuration)
    {
        this.textSnippetRepository = textSnippetRepository;
    }

    public override int DelaySeedingInBackgroundBySeconds => 30;

    protected override async Task InternalSeedData()
    {
        if (Configuration.GetSection("SeedDummyPerformanceTest").Get<bool?>() == true)
            await SeedTextSnippet();
    }

    private async Task SeedTextSnippet()
    {
        var numberOfItemsGroupSeedTextSnippet = 10000;

        if (await textSnippetRepository.CountAsync() >= numberOfItemsGroupSeedTextSnippet)
            return;

        for (var i = 0; i < numberOfItemsGroupSeedTextSnippet; i++)
        {
            using (var uow = UnitOfWorkManager.Begin())
            {
                await textSnippetRepository.CreateOrUpdateAsync(
                    new TextSnippetEntity
                    {
                        Id = Guid.NewGuid(),
                        SnippetText = $"Dummy Abc {i}",
                        FullText = $"This is full text of Dummy Abc {i} snippet text"
                    },
                    p => p.SnippetText == $"Dummy Abc {i}");
                await textSnippetRepository.CreateOrUpdateAsync(
                    new TextSnippetEntity
                    {
                        Id = Guid.NewGuid(),
                        SnippetText = $"Dummy Def {i}",
                        FullText = $"This is full text of Dummy Def {i} snippet text"
                    },
                    p => p.SnippetText == $"Dummy Def {i}");
                await textSnippetRepository.CreateOrUpdateAsync(
                    new TextSnippetEntity
                    {
                        Id = Guid.NewGuid(),
                        SnippetText = $"Dummy Ghi {i}",
                        FullText = $"This is full text of Dummy Ghi {i} snippet text"
                    },
                    p => p.SnippetText == $"Dummy Ghi {i}");

                await uow.CompleteAsync();
            }
        }
    }
}
