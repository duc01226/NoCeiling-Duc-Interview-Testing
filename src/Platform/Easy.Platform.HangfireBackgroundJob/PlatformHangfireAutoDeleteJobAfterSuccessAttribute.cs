using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace Easy.Platform.HangfireBackgroundJob;

/// <summary>
/// This JobFilterAttribute help to auto delete successful job after a period of time
/// </summary>
public class PlatformHangfireAutoDeleteJobAfterSuccessAttribute : JobFilterAttribute, IApplyStateFilter
{
    private readonly TimeSpan deleteAfter;

    public PlatformHangfireAutoDeleteJobAfterSuccessAttribute(int seconds)
    {
        deleteAfter = seconds.Seconds();
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState.Name == "Succeeded")
            context.JobExpirationTimeout = deleteAfter;
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // Do nothing here
    }
}
