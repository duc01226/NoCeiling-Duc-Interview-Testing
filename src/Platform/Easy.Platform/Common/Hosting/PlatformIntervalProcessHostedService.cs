using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Hosting;

public abstract class PlatformIntervalProcessHostedService : PlatformHostedService
{
    public const int DefaultProcessTriggerIntervalTimeMilliseconds = 60000;
    public static readonly ActivitySource ActivitySource = new($"{nameof(PlatformHostedService)}");

    protected readonly SemaphoreSlim IntervalProcessLock = new(1, 1);

    public PlatformIntervalProcessHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    public virtual bool AutoCleanMemory => true;

    public virtual bool LogIntervalProcessInformation => true;

    protected override async Task StartProcess(CancellationToken cancellationToken)
    {
        while (!ProcessStopped && !StoppingCts.IsCancellationRequested)
        {
            try
            {
                await TriggerIntervalProcessAsync(cancellationToken);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "IntervalProcessHostedService {TargetName} FAILED. Error: {Error}", GetType().Name, e.Message);
            }

            await Task.Delay(ProcessTriggerIntervalTime(), cancellationToken);

            if (AutoCleanMemory) Util.GarbageCollector.Collect(immediately: true);
        }
    }

    public virtual async Task TriggerIntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (IntervalProcessLock.CurrentCount == 0) return;

        using (var activity = ActivitySource.StartActivity($"{nameof(PlatformIntervalProcessHostedService)}.{nameof(TriggerIntervalProcessAsync)}"))
        {
            activity?.AddTag("Type", GetType().FullName);

            try
            {
                await IntervalProcessLock.WaitAsync(cancellationToken);

                if (LogIntervalProcessInformation)
                    Logger.LogInformation("IntervalProcessHostedService {TargetName} STARTED", GetType().Name);

                await IntervalProcessAsync(cancellationToken);

                if (LogIntervalProcessInformation)
                    Logger.LogInformation("IntervalProcessHostedService {TargetName} FINISHED", GetType().Name);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "IntervalProcessHostedService {TargetName} FAILED. Error: {Error}", GetType().Name, e.Message);
            }
            finally
            {
                IntervalProcessLock.Release();
            }
        }
    }

    protected abstract Task IntervalProcessAsync(CancellationToken cancellationToken);

    /// <summary>
    /// To config the period of the timer to trigger the <see cref="IntervalProcessAsync" /> method.
    /// Default is one minute TimeSpan.FromMinutes(1)
    /// </summary>
    /// <returns>The configuration as <see cref="TimeSpan" /> type.</returns>
    protected virtual TimeSpan ProcessTriggerIntervalTime()
    {
        return DefaultProcessTriggerIntervalTimeMilliseconds.Milliseconds();
    }
}
