using System.Linq;
using Easy.Platform.Application.Context;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Easy.Platform.RedisCache;

public class PlatformRedisDistributedCacheRepository
    : PlatformCacheRepository, IPlatformDistributedCacheRepository
{
    public static readonly string CachedKeysCollectionName = "___PlatformRedisDistributedCacheKeys___";

    private readonly IPlatformApplicationSettingContext applicationSettingContext;

    protected readonly ILogger<PlatformRedisDistributedCacheRepository> Logger;
    private readonly Lazy<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache> redisCache;
    private bool disposed;

    public PlatformRedisDistributedCacheRepository(
        IServiceProvider serviceProvider,
        IOptions<RedisCacheOptions> optionsAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(serviceProvider)
    {
        this.applicationSettingContext = applicationSettingContext;
        Logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<PlatformRedisDistributedCacheRepository>();
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

        await UpdateGlobalCachedKeys(p => p.Remove(cacheKey, out _));
    }

    public override async Task RemoveAsync(
        Func<PlatformCacheKey, bool> cacheKeyPredicate,
        CancellationToken token = default)
    {
        var globalCachedKeys = await GetGlobalCachedKeysAsync();

        var globalMatchedKeys = globalCachedKeys.Select(p => p.Key).Where(cacheKeyPredicate).ToList();

        if (globalMatchedKeys.Any())
        {
            var clonedMatchedKeys = Util.ListBuilder.New(globalMatchedKeys.ToArray());

            clonedMatchedKeys.ForEach(
                matchedKey =>
                {
                    redisCache.Value.Remove(matchedKey);
                    globalCachedKeys.Remove(matchedKey, out _);
                });

            await SetGlobalCachedKeysAsync(globalCachedKeys);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
            if (redisCache.IsValueCreated)
                redisCache.Value.Dispose();

        disposed = true;
    }

    protected async Task UpdateGlobalCachedKeys(Action<IDictionary<PlatformCacheKey, object>> updateCachedKeysAction)
    {
        var globalCachedKeys = await GetGlobalCachedKeysAsync();

        await globalCachedKeys
            .With(updateCachedKeysAction)
            .Pipe(updatedGlobalCachedKeys => SetGlobalCachedKeysAsync(updatedGlobalCachedKeys));
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

    private async Task<Dictionary<PlatformCacheKey, object>> GetGlobalCachedKeysAsync()
    {
        var cachedKeysList =
            await GetAsync<List<string>>(BuildGlobalCachedKeysDataCacheKey()) ?? new List<string>();

        return cachedKeysList.ToDictionary(
            fullCacheKeyString => (PlatformCacheKey)fullCacheKeyString,
            p => (object)p);
    }

    private PlatformCacheKey BuildGlobalCachedKeysDataCacheKey()
    {
        return new PlatformCacheKey(
            context: applicationSettingContext.ApplicationName,
            collection: CachedKeysCollectionName);
    }

    private async Task SetGlobalCachedKeysAsync(IDictionary<PlatformCacheKey, object> value)
    {
        var cacheKey = BuildGlobalCachedKeysDataCacheKey();
        var cacheValue = value.Keys.Select(p => p.ToString()).ToList();

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
