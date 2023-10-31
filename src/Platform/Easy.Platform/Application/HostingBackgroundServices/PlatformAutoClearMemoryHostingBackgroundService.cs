using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.HostingBackgroundServices;

internal sealed class PlatformAutoClearMemoryHostingBackgroundService : PlatformIntervalProcessHostedService
{
    public PlatformAutoClearMemoryHostingBackgroundService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    public override bool AutoCleanMemory => false;

    public override bool LogIntervalProcessInformation => false;

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 5.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await Task.Run(
            () =>
            {
                GC.Collect();
                Util.GarbageCollector.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true, immediately: true);
            },
            cancellationToken);
    }
}
