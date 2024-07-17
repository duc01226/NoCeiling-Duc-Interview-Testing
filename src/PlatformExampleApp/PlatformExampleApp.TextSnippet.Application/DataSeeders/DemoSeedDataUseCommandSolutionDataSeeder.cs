using Easy.Platform.Application;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Dtos.EntityDtos;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.DataSeeders;

/// <summary>
/// Use command to seed data is also like real testing the command too
/// </summary>
public sealed class DemoSeedDataUseCommandSolutionDataSeeder : PlatformApplicationDataSeeder
{
    public DemoSeedDataUseCommandSolutionDataSeeder(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(unitOfWorkManager, serviceProvider, configuration, loggerFactory, rootServiceProvider)
    {
    }

    public override int DelaySeedingInBackgroundBySeconds => DefaultActiveDelaySeedingInBackgroundBySeconds;

    public override int SeedOrder => 2;

    protected override async Task InternalSeedData(bool isReplaceNewSeed = false)
    {
        await ServiceProvider.ExecuteInjectScopedAsync(SeedSnippetText, isReplaceNewSeed);
    }

    private static async Task SeedSnippetText(
        bool isReplaceNewSeed,
        IPlatformCqrs cqrs,
        IPlatformApplicationRequestContextAccessor userContextAccessor,
        ITextSnippetRepository<TextSnippetEntity> snippetRepository)
    {
        if (await snippetRepository.AnyAsync(p => p.SnippetText == "Dummy Seed SnippetText") && !isReplaceNewSeed) return;

        userContextAccessor.Current.SetUserId(Ulid.NewUlid().ToString());
        userContextAccessor.Current.SetEmail("SeedUserEmail");

        await cqrs.SendCommand(
            new SaveSnippetTextCommand
            {
                Data = new TextSnippetEntityDto
                {
                    Id = Ulid.Parse("01J0P1CE4TW4RY3TKZ9CNX73NR").ToString(),
                    SnippetText = "Dummy Seed SnippetText",
                    FullText = "Dummy Seed FullText"
                },
                AutoCreateIfNotExisting = true
            });
    }
}
