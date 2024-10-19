using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Easy.Platform.Infrastructures.Caching.BuiltInCacheRepositories;

public class PlatformMemoryCacheRepository : PlatformCacheRepository, IPlatformMemoryCacheRepository
{
    private readonly MemoryDistributedCache memoryDistributedCache;

    public PlatformMemoryCacheRepository(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        PlatformCacheSettings cacheSettings) : base(serviceProvider, loggerFactory, cacheSettings)
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
        SetAsync(cacheKey, value, cacheOptions).WaitResult();
    }

    public override async Task SetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        await CacheSettings.ExecuteWithSlowWarning(
            async () =>
            {
                await Util.TaskRunner.WhenAll(
                    SetToMemoryDistributedCacheAsync(cacheKey, value, cacheOptions, token),
                    UpdateGlobalCachedKeys(p => p.TryAdd(cacheKey, null)));
            },
            () => Logger,
            true);
    }

    public override async Task RemoveAsync(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        await CacheSettings.ExecuteWithSlowWarning(
            async () =>
            {
                try
                {
                    await memoryDistributedCache.RemoveAsync(cacheKey, token);
                }
                catch (Exception ex)
                {
                    throw new Exception($"{GetType().Name} RemoveAsync failed. [CacheKey: {cacheKey}]. {ex.Message}", ex);
                }

                await UpdateGlobalCachedKeys(p => p.TryRemove(cacheKey, out var _));
            },
            () => Logger,
            true);
    }

    public override async Task RemoveAsync(Func<PlatformCacheKey, bool> cacheKeyPredicate, CancellationToken token = default)
    {
        var allCachedKeys = await LoadGlobalAllRequestCachedKeys();

        var globalMatchedKeys = allCachedKeys.Select(p => p.Key).Where(cacheKeyPredicate).ToList();

        if (globalMatchedKeys.Any())
        {
            var clonedMatchedKeys = globalMatchedKeys.ToArray();

            clonedMatchedKeys.ForEach(
                matchedKey =>
                {
                    memoryDistributedCache.Remove(matchedKey);
                    allCachedKeys.TryRemove(matchedKey, out var _);
                });

            await SetGlobalCachedKeysAsync(allCachedKeys);
        }
    }

    public override async Task ProcessClearDeprecatedGlobalRequestCachedKeys()
    {
        var toUpdateRequestCachedKeys = await LoadGlobalAllRequestCachedKeys();

        await toUpdateRequestCachedKeys.SelectList(p => p.Key)
            .ForEachAsync(
                async key =>
                {
                    if (await memoryDistributedCache.GetAsync(key) == null) toUpdateRequestCachedKeys.Remove(key, out _);
                });

        await SetGlobalCachedKeysAsync(toUpdateRequestCachedKeys);
    }

    protected async Task UpdateGlobalCachedKeys(Action<ConcurrentDictionary<PlatformCacheKey, object>> updateCachedKeysAction)
    {
        var currentGlobalAllRequestCachedKeys = await LoadGlobalAllRequestCachedKeys();

        await currentGlobalAllRequestCachedKeys
            .With(updateCachedKeysAction)
            .Pipe(SetGlobalCachedKeysAsync);
    }

    private async Task SetGlobalCachedKeysAsync(ConcurrentDictionary<PlatformCacheKey, object> globalCachedKeys)
    {
        var cacheKey = GetGlobalAllRequestCachedKeysCacheKey();
        var cacheValue = globalCachedKeys.Select(p => p.Key).ToList();

        await SetToMemoryDistributedCacheAsync(
            cacheKey,
            cacheValue,
            new PlatformCacheEntryOptions
            {
                UnusedExpirationInSeconds = null,
                AbsoluteExpirationInSeconds = null
            });
    }

    private async Task SetToMemoryDistributedCacheAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        try
        {
            await memoryDistributedCache.SetAsync(
                cacheKey,
                PlatformJsonSerializer.SerializeToUtf8Bytes(value),
                MapToDistributedCacheEntryOptions(cacheOptions),
                token);
        }
        catch (Exception ex)
        {
            throw new Exception($"{GetType().Name} SetToMemoryDistributedCacheAsync failed. [CacheKey: {cacheKey}]. {ex.Message}", ex);
        }
    }

    public override PlatformCacheKey GetGlobalAllRequestCachedKeysCacheKey()
    {
        return new PlatformCacheKey(collection: CachedKeysCollectionName);
    }
}
