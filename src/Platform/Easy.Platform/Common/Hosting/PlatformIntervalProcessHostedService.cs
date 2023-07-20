using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Hosting;

public abstract class PlatformIntervalProcessHostedService : PlatformHostedService
{
    protected readonly SemaphoreSlim IntervalProcessLock = new(1, 1);

    public PlatformIntervalProcessHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    protected override async Task StartProcess(CancellationToken cancellationToken)
    {
        while (!ProcessStopped && !StoppingCts.IsCancellationRequested)
        {
            await TriggerIntervalProcessAsync(cancellationToken);
            await Task.Delay(ProcessTriggerIntervalTime(), cancellationToken);
        }
    }

    public async Task TriggerIntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (IntervalProcessLock.CurrentCount == 0) return;

        try
        {
            await IntervalProcessLock.WaitAsync(cancellationToken);

            await IntervalProcessAsync(cancellationToken);
        }
        finally
        {
            IntervalProcessLock.Release();
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
        return 1.Minutes();
    }
}
