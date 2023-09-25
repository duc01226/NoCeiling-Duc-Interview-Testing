using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.Caching;

public class PlatformAutoClearDeprecatedGlobalRequestCachedKeysBackgroundService : PlatformIntervalProcessHostedService
{
    private readonly IPlatformCacheRepositoryProvider cacheRepositoryProvider;

    public PlatformAutoClearDeprecatedGlobalRequestCachedKeysBackgroundService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(serviceProvider, loggerFactory)
    {
        this.cacheRepositoryProvider = cacheRepositoryProvider;
    }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 10.Minutes();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await (cacheRepositoryProvider.TryGet(PlatformCacheRepositoryType.Distributed)?.ProcessClearDeprecatedGlobalRequestCachedKeys() ?? Task.CompletedTask);
        await (cacheRepositoryProvider.TryGet(PlatformCacheRepositoryType.Memory)?.ProcessClearDeprecatedGlobalRequestCachedKeys() ?? Task.CompletedTask);
    }
}
