using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.HostingBackgroundServices;

internal sealed class PlatformAutoClearMemoryHostingBackgroundService : PlatformIntervalHostingBackgroundService
{
    public const int DefaultProcessTriggerIntervalTimeSeconds = 5;

    public PlatformAutoClearMemoryHostingBackgroundService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        int processTriggerIntervalTimeSeconds = DefaultProcessTriggerIntervalTimeSeconds) : base(serviceProvider, loggerFactory)
    {
        ProcessTriggerIntervalTimeSeconds = processTriggerIntervalTimeSeconds;
    }

    public override bool ActivateTracing => false;

    public override bool LogIntervalProcessInformation => false;

    public int ProcessTriggerIntervalTimeSeconds { get; }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return ProcessTriggerIntervalTimeSeconds.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        Util.GarbageCollector.Collect(0, true);
    }
}
