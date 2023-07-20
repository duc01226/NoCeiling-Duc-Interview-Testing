using System.Collections.Concurrent;
using Easy.Platform.Application.Context;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Caching.Distributed;
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
        ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
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
        await SetToRedisCacheAsync(cacheKey, value, cacheOptions, token);

        await UpdateGlobalCachedKeys(p => p.TryAdd(cacheKey, null));
    }

    public override async Task RemoveAsync(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        // Store stack trace before call redisCache.Value.GetAsync to keep the original stack trace to log
        // after redisCache.Value.GetAsync will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = Environment.StackTrace;

        try
        {
            await redisCache.Value.RemoveAsync(cacheKey, token);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "RemoveAsync failed. [[Exception:{Exception}]]. [CacheKey: {CacheKey}]. [[FullStackTrace:{FullStackTrace}]]",
                ex.ToString(),
                cacheKey,
                $"{ex.StackTrace}{Environment.NewLine}FromFullStackTrace:{fullStackTrace}");

            throw new Exception($"{GetType().Name} RemoveAsync failed. {ex.Message}", ex);
        }

        await UpdateGlobalCachedKeys(p => p.TryRemove(cacheKey, out var _));
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

    public override PlatformCacheKey GetGlobalAllRequestCachedKeysCacheKey()
    {
        return new PlatformCacheKey(
            context: applicationSettingContext.ApplicationName,
            collection: CachedKeysCollectionName);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing && redisCache.IsValueCreated)
            redisCache.Value.Dispose();

        disposed = true;
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
        // Store stack trace before call redisCache.Value.SetAsync to keep the original stack trace to log
        // after redisCache.Value.SetAsync will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = Environment.StackTrace;

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
                "SetToRedisCacheAsync failed. [[Exception:{Exception}]]. [CacheKey: {CacheKey}]. [[FullStackTrace:{FullStackTrace}]]",
                ex.ToString(),
                cacheKey,
                $"{ex.StackTrace}{Environment.NewLine}FromFullStackTrace:{fullStackTrace}");

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
