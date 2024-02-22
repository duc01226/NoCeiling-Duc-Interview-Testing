using System.Collections.Concurrent;
using Easy.Platform.Application;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Easy.Platform.RedisCache;

public class PlatformRedisDistributedCacheRepository : PlatformCacheRepository, IPlatformDistributedCacheRepository
{
    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly Lazy<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache> redisCache;
    private bool disposed;

    public PlatformRedisDistributedCacheRepository(
        IServiceProvider serviceProvider,
        IOptions<RedisCacheOptions> optionsAccessor,
        IPlatformApplicationSettingContext applicationSettingContext,
        ILoggerFactory loggerFactory,
        PlatformCacheSettings cacheSettings) : base(serviceProvider, loggerFactory, cacheSettings)
    {
        this.applicationSettingContext = applicationSettingContext;
        redisCache = new Lazy<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache>(
            () => new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache(optionsAccessor));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public override T Get<T>(PlatformCacheKey cacheKey)
    {
        return GetAsync<T>(cacheKey).GetResult();
    }

    public override async Task<T> GetAsync<T>(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        using (var activity = IPlatformCacheRepository.ActivitySource.StartActivity($"DistributedCache.{nameof(GetAsync)}"))
        {
            activity?.AddTag("cacheKey", cacheKey);

            return await CacheSettings.ExecuteWithSlowWarning(
                async () =>
                {
                    try
                    {
                        var result = await redisCache.Value.GetAsync(cacheKey, token);

                        try
                        {
                            return result == null ? default : PlatformJsonSerializer.Deserialize<T>(result);
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(e, "GetAsync failed. CacheKey:{CacheKey}", cacheKey);
                            // WHY: If parse failed, the cached data could be obsolete. Then just clear the cache
                            await RemoveAsync(cacheKey, token);
                            return default;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{GetType().Name} GetAsync failed. {e.Message}.", e);
                    }
                },
                () => Logger);
        }
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
        using (var activity = IPlatformCacheRepository.ActivitySource.StartActivity($"DistributedCache.{nameof(SetAsync)}"))
        {
            activity?.AddTag("cacheKey", cacheKey);

            await CacheSettings.ExecuteWithSlowWarning(
                async () =>
                {
                    await Util.TaskRunner.WhenAll(
                        SetToRedisCacheAsync(cacheKey, value, cacheOptions, token),
                        UpdateGlobalCachedKeys(p => p.TryAdd(cacheKey, null)));
                },
                () => Logger,
                true);
        }
    }

    public override async Task RemoveAsync(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        await CacheSettings.ExecuteWithSlowWarning(
            async () =>
            {
                try
                {
                    await redisCache.Value.RemoveAsync(cacheKey, token);
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        ex,
                        "RemoveAsync failed. [[Exception:{Exception}]]. [CacheKey: {CacheKey}]",
                        ex.ToString(),
                        cacheKey);

                    throw new Exception($"{GetType().Name} RemoveAsync failed. {ex.Message}", ex);
                }

                await UpdateGlobalCachedKeys(p => p.TryRemove(cacheKey, out var _));
            },
            () => Logger,
            true);
    }

    public override async Task RemoveAsync(
        Func<PlatformCacheKey, bool> cacheKeyPredicate,
        CancellationToken token = default)
    {
        var allCachedKeys = await LoadGlobalAllRequestCachedKeys();

        var globalMatchedKeys = allCachedKeys.Select(p => p.Key).Where(cacheKeyPredicate).ToList();

        if (globalMatchedKeys.Any())
        {
            var clonedMatchedKeys = Util.ListBuilder.New(globalMatchedKeys.ToArray());

            clonedMatchedKeys.ForEach(
                matchedKey =>
                {
                    redisCache.Value.Remove(matchedKey);
                    allCachedKeys.TryRemove(matchedKey, out var _);
                });

            await SetGlobalCachedKeysAsync(allCachedKeys);
        }
    }

    public override async Task ProcessClearDeprecatedGlobalRequestCachedKeys()
    {
        var toUpdateRequestCachedKeys = await LoadGlobalAllRequestCachedKeys();

        await toUpdateRequestCachedKeys.SelectList(p => p.Key).ForEachAsync(
            async key =>
            {
                if (await redisCache.Value.GetAsync(key) == null) toUpdateRequestCachedKeys.Remove(key, out _);
            });

        await SetGlobalCachedKeysAsync(toUpdateRequestCachedKeys);
    }

    public override PlatformCacheKey GetGlobalAllRequestCachedKeysCacheKey()
    {
        return new PlatformCacheKey(
            context: applicationSettingContext.ApplicationName,
            collection: CachedKeysCollectionName);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
                // Release managed resources
                redisCache.PipeAction(
                    _ =>
                    {
                        if (redisCache.IsValueCreated) redisCache.Value.Dispose();
                    });

            // Release unmanaged resources

            disposed = true;
        }
    }

    protected async Task UpdateGlobalCachedKeys(Action<ConcurrentDictionary<PlatformCacheKey, object>> updateCachedKeysAction)
    {
        var currentGlobalAllRequestCachedKeys = await LoadGlobalAllRequestCachedKeys();

        await currentGlobalAllRequestCachedKeys
            .With(updateCachedKeysAction)
            .Pipe(SetGlobalCachedKeysAsync);
    }

    private async Task SetToRedisCacheAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        try
        {
            await redisCache.Value.SetAsync(
                cacheKey,
                PlatformJsonSerializer.SerializeToUtf8Bytes(value),
                MapToDistributedCacheEntryOptions(cacheOptions),
                token);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "SetToRedisCacheAsync failed. [[Exception:{Exception}]]. [CacheKey: {CacheKey}]",
                ex.ToString(),
                cacheKey);

            throw new Exception($"{GetType().Name} SetToRedisCacheAsync failed. {ex.Message}", ex);
        }
    }

    private async Task SetGlobalCachedKeysAsync(ConcurrentDictionary<PlatformCacheKey, object> globalCachedKeys)
    {
        var cacheKey = GetGlobalAllRequestCachedKeysCacheKey();
        var cacheValue = globalCachedKeys.Select(p => p.Key).ToList();

        await SetToRedisCacheAsync(
            cacheKey,
            cacheValue,
            new PlatformCacheEntryOptions
            {
                UnusedExpirationInSeconds = null,
                AbsoluteExpirationInSeconds = null
            });
    }
}
