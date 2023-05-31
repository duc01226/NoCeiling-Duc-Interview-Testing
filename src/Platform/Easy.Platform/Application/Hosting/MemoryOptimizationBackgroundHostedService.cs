using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Hosting;

public class MemoryOptimizationBackgroundHostedService : PlatformIntervalProcessHostedService
{
    public MemoryOptimizationBackgroundHostedService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        GC.Collect();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 30.Seconds();
    }
}
