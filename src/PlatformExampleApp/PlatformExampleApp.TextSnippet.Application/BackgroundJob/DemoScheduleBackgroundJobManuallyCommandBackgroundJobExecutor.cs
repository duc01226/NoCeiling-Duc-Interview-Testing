using Easy.Platform.Application.BackgroundJob;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.BackgroundJob;

public sealed class DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutor
    : PlatformApplicationBackgroundJobExecutor<DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam>
{
    private readonly ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository;

    public DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutor(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository) : base(
        unitOfWorkManager,
        loggerFactory,
        rootServiceProvider)
    {
        this.textSnippetEntityRepository = textSnippetEntityRepository;
    }

    public override async Task ProcessAsync(
        DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam param)
    {
        await textSnippetEntityRepository.CreateOrUpdateAsync(
            TextSnippetEntity.Create(
                id: Ulid.Parse("01J0P1B1HNKNVK5XV31D837KGQ").ToString(),
                snippetText: $"DemoScheduleBackgroundJobManually {Clock.Now.ToShortTimeString()} {param.NewSnippetText ?? ""}",
                fullText: $"DemoScheduleBackgroundJobManually {param.NewSnippetText ?? ""}"));
    }
}

public sealed class DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam
{
    public string NewSnippetText { get; set; }
}
