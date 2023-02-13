using System.Collections.Concurrent;
using System.Linq;
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
    private bool disposed;
    private readonly Lazy<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache> redisCache;

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
        var result = await redisCache.Value.GetAsync(cacheKey, token);

        try
        {
            return result == null ? default : PlatformJsonSerializer.Deserialize<T>(result);
        }
        catch (Exception)
        {
            // WHY: If parse failed, the cached data could be obsolete. Then just clear the cache
            await RemoveAsync(cacheKey, token);
            return default;
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
        await redisCache.Value.RemoveAsync(cacheKey, token);

        await UpdateGlobalCachedKeys(p => p.TryRemove(cacheKey, out var _));
    }

    public override async Task RemoveAsync(
        Func<PlatformCacheKey, bool> cacheKeyPredicate,
        CancellationToken token = default)
    {
        var allCachedKeys = GlobalAllRequestCachedKeys.Value;

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
        await GlobalAllRequestCachedKeys.Value
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
                $"[{GetType().Name}] SetToRedisCache has errors. CacheKey: {{CacheKey}}. Value: {{CacheValue}}",
                cacheKey,
                value.AsJson());
            throw;
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
