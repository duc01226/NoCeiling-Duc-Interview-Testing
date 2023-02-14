using Easy.Platform.Application;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.DataSeeders;

public class DemoSeedDataUseCommandSolutionForSyncDataByMessageBusDataSeeder : PlatformApplicationDataSeeder
{
    public DemoSeedDataUseCommandSolutionForSyncDataByMessageBusDataSeeder(
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(unitOfWorkManager, serviceProvider, configuration)
    {
    }

    public override int DelaySeedingInBackgroundBySeconds => 30;

    public override int SeedOrder => 2;

    protected override async Task InternalSeedData()
    {
        await ServiceProvider.ExecuteInjectScopedAsync(SeedSnippetText);
    }

    private static async Task SeedSnippetText(
        IPlatformCqrs cqrs,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        ITextSnippetRepository<TextSnippetEntity> snippetRepository)
    {
        if (await snippetRepository.AnyAsync(p => p.SnippetText == "Dummy Seed SnippetText")) return;

        userContextAccessor.Current.SetUserId(Guid.NewGuid().ToString());
        userContextAccessor.Current.SetEmail("SeedUserEmail");

        await cqrs.SendCommand(
            new SaveSnippetTextCommand
            {
                Data = new TextSnippetEntityDto { SnippetText = "Dummy Seed SnippetText", FullText = "Dummy Seed FullText" }
            });
    }
}
