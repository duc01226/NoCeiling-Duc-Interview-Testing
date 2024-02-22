using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.HostingBackgroundServices;

internal sealed class PlatformAutoClearMemoryHostingBackgroundService : PlatformIntervalHostingBackgroundService
{
    public PlatformAutoClearMemoryHostingBackgroundService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    public override bool AutoCleanMemory => false;

    public override bool ActivateTracing => false;

    public override bool LogIntervalProcessInformation => false;

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 10.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        Util.GarbageCollector.Collect(aggressiveImmediately: true);
    }
}
