using Easy.Platform.Application.BackgroundJob;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.BackgroundJob;

public class DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutor
    : PlatformApplicationBackgroundJobExecutor<DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam>
{
    private readonly ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository;

    public DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory,
        ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository) : base(
        unitOfWorkManager,
        loggerFactory)
    {
        this.textSnippetEntityRepository = textSnippetEntityRepository;
    }

    public override async Task ProcessAsync(
        DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam param = null)
    {
        await textSnippetEntityRepository.CreateOrUpdateAsync(
            new TextSnippetEntity
            {
                Id = Guid.Parse("90d8898b-c232-461e-9cb0-3242ac6c5b41"),
                SnippetText =
                    $"DemoScheduleBackgroundJobManually {Clock.Now.ToShortTimeString()} {param.NewSnippetText ?? ""}",
                FullText = $"DemoScheduleBackgroundJobManually {param.NewSnippetText ?? ""}"
            });
    }
}

public class DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam
{
    public string NewSnippetText { get; set; }
}
