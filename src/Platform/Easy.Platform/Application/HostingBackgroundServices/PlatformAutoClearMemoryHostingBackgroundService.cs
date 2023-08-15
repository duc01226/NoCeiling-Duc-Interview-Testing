using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.HostingBackgroundServices;

internal sealed class PlatformAutoClearMemoryHostingBackgroundService : PlatformIntervalProcessHostedService
{
    public PlatformAutoClearMemoryHostingBackgroundService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    public override bool AutoCleanMemory => false;

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 10.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        GC.Collect();
        PlatformGlobal.MemoryCollector.CollectGarbageMemory(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }
}
