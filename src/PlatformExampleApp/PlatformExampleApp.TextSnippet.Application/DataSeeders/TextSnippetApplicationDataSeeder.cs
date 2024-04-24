using Easy.Platform.Application;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.DataSeeders;

public sealed class TextSnippetApplicationDataSeeder : PlatformApplicationDataSeeder
{
    private readonly ITextSnippetRootRepository<MultiDbDemoEntity> multiDbDemoEntityRepository;
    private readonly ITextSnippetRootRepository<TextSnippetEntity> textSnippetRepository;

    public TextSnippetApplicationDataSeeder(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        ITextSnippetRootRepository<TextSnippetEntity> textSnippetRepository,
        ITextSnippetRootRepository<MultiDbDemoEntity> multiDbDemoEntityRepository) : base(
        unitOfWorkManager,
        serviceProvider,
        configuration,
        loggerFactory,
        rootServiceProvider)
    {
        this.textSnippetRepository = textSnippetRepository;
        this.multiDbDemoEntityRepository = multiDbDemoEntityRepository;
    }

    protected override async Task InternalSeedData(bool isReplaceNewSeed = false)
    {
        await SeedTextSnippet();

        await SeedMultiDbDemoEntity();
    }

    private async Task SeedMultiDbDemoEntity()
    {
        if (await textSnippetRepository.AnyAsync(p => p.SnippetText.StartsWith("Example")))
            return;

        for (var i = 0; i < 20; i++)
        {
            await multiDbDemoEntityRepository.CreateOrUpdateAsync(
                new MultiDbDemoEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Multi Db Demo Entity {i}"
                });
        }
    }

    private async Task SeedTextSnippet()
    {
        var numberOfItemsGroupSeedTextSnippet = 20;

        if (await textSnippetRepository.CountAsync() >= numberOfItemsGroupSeedTextSnippet)
            return;

        for (var i = 0; i < numberOfItemsGroupSeedTextSnippet; i++)
        {
            await textSnippetRepository.CreateOrUpdateAsync(
                TextSnippetEntity.Create(id: Guid.NewGuid(), snippetText: $"Example Abc {i}", fullText: $"This is full text of Example Abc {i} snippet text"),
                customCheckExistingPredicate: p => p.SnippetText == $"Example Abc {i}");
            await textSnippetRepository.CreateOrUpdateAsync(
                TextSnippetEntity.Create(id: Guid.NewGuid(), snippetText: $"Example Def {i}", fullText: $"This is full text of Example Def {i} snippet text"),
                customCheckExistingPredicate: p => p.SnippetText == $"Example Def {i}");
            await textSnippetRepository.CreateOrUpdateAsync(
                TextSnippetEntity.Create(id: Guid.NewGuid(), snippetText: $"Example Ghi {i}", fullText: $"This is full text of Example Ghi {i} snippet text"),
                customCheckExistingPredicate: p => p.SnippetText == $"Example Ghi {i}");
        }
    }
}
