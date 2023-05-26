using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Easy.Platform.Infrastructures.Caching.BuiltInCacheRepositories;

public sealed class PlatformMemoryCacheRepository : PlatformCacheRepository, IPlatformMemoryCacheRepository
{
    private readonly MemoryDistributedCache memoryDistributedCache;

    public PlatformMemoryCacheRepository(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) : base(serviceProvider, loggerFactory)
    {
        memoryDistributedCache = new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()),
            loggerFactory);
    }

    public override T Get<T>(PlatformCacheKey cacheKey)
    {
        var result = memoryDistributedCache.Get(cacheKey);
        return result == null ? default : PlatformJsonSerializer.Deserialize<T>(result);
    }

    public override async Task<T> GetAsync<T>(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        var result = await memoryDistributedCache.GetAsync(cacheKey, token);
        return result == null ? default : PlatformJsonSerializer.Deserialize<T>(result);
    }

    public override void Set<T>(PlatformCacheKey cacheKey, T value, PlatformCacheEntryOptions cacheOptions = null)
    {
        memoryDistributedCache.Set(
            cacheKey,
            PlatformJsonSerializer.SerializeToUtf8Bytes(value),
            MapToDistributedCacheEntryOptions(cacheOptions));

        GlobalAllRequestCachedKeys.Value.TryAdd(cacheKey, null);
    }

    public override async Task SetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        await memoryDistributedCache.SetAsync(
            cacheKey,
            PlatformJsonSerializer.SerializeToUtf8Bytes(value),
            MapToDistributedCacheEntryOptions(cacheOptions ?? GetDefaultCacheEntryOptions()),
            token);

        GlobalAllRequestCachedKeys.Value.TryAdd(cacheKey, null);
    }

    public override async Task RemoveAsync(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        await memoryDistributedCache.RemoveAsync(cacheKey, token);

        GlobalAllRequestCachedKeys.Value.Remove(cacheKey, out _);
    }

    public override async Task RemoveAsync(Func<PlatformCacheKey, bool> cacheKeyPredicate, CancellationToken token = default)
    {
        var toDeleteKeys = GlobalAllRequestCachedKeys.Value.Where(p => cacheKeyPredicate(p.Key)).Select(p => p.Key).ToList();

        await toDeleteKeys.ForEachAsync(cacheKey => memoryDistributedCache.RemoveAsync(cacheKey, token));

        toDeleteKeys.ForEach(cacheKey => GlobalAllRequestCachedKeys.Value.Remove(cacheKey, out _));
    }

    public override PlatformCacheKey GetGlobalAllRequestCachedKeysCacheKey()
    {
        return new PlatformCacheKey(collection: CachedKeysCollectionName);
    }

    private DistributedCacheEntryOptions MapToDistributedCacheEntryOptions(PlatformCacheEntryOptions options)
    {
        var result = new DistributedCacheEntryOptions();

        var absoluteExpirationRelativeToNow = options?.AbsoluteExpirationRelativeToNow();
        if (absoluteExpirationRelativeToNow != null)
            result.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;

        var slidingExpiration = options?.SlidingExpiration();
        if (slidingExpiration != null)
            result.SlidingExpiration = slidingExpiration;

        return result;
    }
}
