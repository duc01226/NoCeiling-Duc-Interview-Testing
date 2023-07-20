using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.BackgroundJob;
using PlatformExampleApp.TextSnippet.Application.BackgroundJob;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseCommands.OtherDemos;

public sealed class DemoScheduleBackgroundJobManuallyCommand : PlatformCqrsCommand<DemoScheduleBackgroundJobManuallyCommandResult>
{
    public static readonly string DefaultUpdateTextSnippetFullText =
        "DemoScheduleBackgroundJobManually NewSnippetText";

    public string NewSnippetText { get; set; } = DefaultUpdateTextSnippetFullText;
}

public sealed class DemoScheduleBackgroundJobManuallyCommandResult : PlatformCqrsCommandResult
{
    public string ScheduledJobId { get; set; }
}

internal sealed class DemoScheduleBackgroundJobManuallyCommandHandler
    : PlatformCqrsCommandApplicationHandler<DemoScheduleBackgroundJobManuallyCommand, DemoScheduleBackgroundJobManuallyCommandResult>
{
    private readonly IPlatformBackgroundJobScheduler backgroundJobScheduler;

    public DemoScheduleBackgroundJobManuallyCommandHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        IPlatformBackgroundJobScheduler backgroundJobScheduler) : base(userContext, unitOfWorkManager, cqrs)
    {
        this.backgroundJobScheduler = backgroundJobScheduler;
    }

    protected override async Task<DemoScheduleBackgroundJobManuallyCommandResult> HandleAsync(
        DemoScheduleBackgroundJobManuallyCommand request,
        CancellationToken cancellationToken)
    {
        var scheduledJobId = backgroundJobScheduler
            .Schedule<DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutor, DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam>(
                new DemoScheduleBackgroundJobManuallyCommandBackgroundJobExecutorParam
                {
                    NewSnippetText = request.NewSnippetText
                },
                delay: TimeSpan.Zero);

        return new DemoScheduleBackgroundJobManuallyCommandResult
        {
            ScheduledJobId = scheduledJobId
        };
    }
}
