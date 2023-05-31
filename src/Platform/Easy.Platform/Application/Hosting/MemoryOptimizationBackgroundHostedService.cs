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
        for (var i = 0; i <= GC.MaxGeneration; i++)
        {
            if (i == GC.MaxGeneration)
                GC.Collect(i, GCCollectionMode.Aggressive, true, true);
            else
                GC.Collect(i, GCCollectionMode.Forced);
        }
    }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 30.Seconds();
    }
}
