using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.Caching;

public interface IPlatformCacheRepository
{
    /// <summary>
    /// Gets a value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <returns>The located value or null.</returns>
    T Get<T>(PlatformCacheKey cacheKey);

    /// <summary>
    /// Gets a value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task" /> that represents the asynchronous operation, containing the located value or null.</returns>
    Task<T> GetAsync<T>(PlatformCacheKey cacheKey, CancellationToken token = default);

    /// <summary>
    /// Sets a value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="cacheOptions">The cache options for the value.</param>
    void Set<T>(PlatformCacheKey cacheKey, T value, PlatformCacheEntryOptions cacheOptions = null);

    /// <summary>
    /// Sets the value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="cacheOptions">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task" /> that represents the asynchronous operation.</returns>
    Task SetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default);

    /// <summary>
    /// Sets a value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="absoluteExpirationInSeconds">The absoluteExpirationInSeconds cache options for the value.</param>
    void Set<T>(PlatformCacheKey cacheKey, T value, double? absoluteExpirationInSeconds = null);

    /// <summary>
    /// Sets the value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="absoluteExpirationInSeconds">The absoluteExpirationInSeconds cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task" /> that represents the asynchronous operation.</returns>
    Task SetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        double? absoluteExpirationInSeconds = null,
        CancellationToken token = default);

    /// <summary>
    /// Removes the value with the given key.
    /// </summary>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task" /> that represents the asynchronous operation.</returns>
    Task RemoveAsync(PlatformCacheKey cacheKey, CancellationToken token = default);

    /// <summary>
    /// Removes the value with the given key predicate.
    /// </summary>
    /// <param name="cacheKeyPredicate">A string identifying the requested value predicate.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task" /> that represents the asynchronous operation.</returns>
    Task RemoveAsync(Func<PlatformCacheKey, bool> cacheKeyPredicate, CancellationToken token = default);

    /// <summary>
    /// Removes the all cached value of collection with the given CollectionCacheKeyProvider.
    /// </summary>
    Task RemoveCollectionAsync<TCollectionCacheKeyProvider>(CancellationToken token = default)
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider;

    /// <summary>
    /// Return cache from request function if exist. If not, call request function to get data, cache the data and return it.
    /// </summary>
    /// <param name="request">The request function return data to set in the cache.</param>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="cacheOptions">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        PlatformCacheKey cacheKey,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default);

    /// <summary>
    /// Return cache from request function if exist. If not, call request function to get data, cache the data and return it.
    /// </summary>
    /// <param name="request">The request function return data to set in the cache.</param>
    /// <param name="cacheKey">A string identifying the requested value.</param>
    /// <param name="absoluteExpirationInSeconds">The absoluteExpirationInSeconds cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        PlatformCacheKey cacheKey,
        double? absoluteExpirationInSeconds = null,
        CancellationToken token = default);

    /// <summary>
    /// Return default cache entry options value. This could be config when register module, override <see cref="PlatformCachingModule.DefaultPlatformCacheEntryOptions" />
    /// </summary>
    PlatformCacheEntryOptions GetDefaultCacheEntryOptions();
}

public abstract class PlatformCacheRepository : IPlatformCacheRepository
{
    public static readonly string CachedKeysCollectionName = "___PlatformGlobalCacheKeys___";
    protected readonly ILogger Logger;

    private readonly IServiceProvider serviceProvider;

    public PlatformCacheRepository(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        this.serviceProvider = serviceProvider;
        Logger = loggerFactory.CreateLogger(typeof(PlatformCacheRepository));
    }

    public abstract T Get<T>(PlatformCacheKey cacheKey);

    public abstract Task<T> GetAsync<T>(PlatformCacheKey cacheKey, CancellationToken token = default);

    public abstract void Set<T>(PlatformCacheKey cacheKey, T value, PlatformCacheEntryOptions cacheOptions = null);

    public abstract Task SetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default);

    public void Set<T>(PlatformCacheKey cacheKey, T value, double? absoluteExpirationInSeconds = null)
    {
        var defaultCacheOptions = GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        Set(cacheKey, value, defaultCacheOptions);
    }

    public async Task SetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        double? absoluteExpirationInSeconds = null,
        CancellationToken token = default)
    {
        var defaultCacheOptions = GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        await SetAsync(
            cacheKey,
            value,
            defaultCacheOptions,
            token);
    }

    public abstract Task RemoveAsync(PlatformCacheKey cacheKey, CancellationToken token = default);

    public abstract Task RemoveAsync(
        Func<PlatformCacheKey, bool> cacheKeyPredicate,
        CancellationToken token = default);

    public async Task RemoveCollectionAsync<TCollectionCacheKeyProvider>(CancellationToken token = default)
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
    {
        await RemoveAsync(
            serviceProvider.GetService<TCollectionCacheKeyProvider>()!.MatchCollectionKeyPredicate(),
            token);
    }

    public async Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        PlatformCacheKey cacheKey,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        var cachedData = await GetAsync<TData>(cacheKey, token);

        return cachedData ?? await RequestAndCacheNewData();

        async Task<TData> RequestAndCacheNewData()
        {
            var requestedData = await request();

            await SetAsync(
                cacheKey,
                requestedData,
                cacheOptions,
                token);

            return requestedData;
        }
    }

    public Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        PlatformCacheKey cacheKey,
        double? absoluteExpirationInSeconds = null,
        CancellationToken token = default)
    {
        return CacheRequestAsync(
            request,
            cacheKey,
            new PlatformCacheEntryOptions
            {
                AbsoluteExpirationInSeconds = absoluteExpirationInSeconds ??
                                              PlatformCacheEntryOptions.DefaultExpirationInSeconds
            },
            token);
    }

    public PlatformCacheEntryOptions GetDefaultCacheEntryOptions()
    {
        return serviceProvider.GetService<PlatformCacheEntryOptions>() ?? new PlatformCacheEntryOptions();
    }

    /// <summary>
    /// Used to build a unique cache key to store list of all request cached keys
    /// </summary>
    public abstract PlatformCacheKey GetGlobalAllRequestCachedKeysCacheKey();

    protected async Task<ConcurrentDictionary<PlatformCacheKey, object>> LoadGlobalAllRequestCachedKeys()
    {
        try
        {
            return await GetAsync<List<PlatformCacheKey>>(cacheKey: GetGlobalAllRequestCachedKeysCacheKey())
                .Then(_ => _ ?? new List<PlatformCacheKey>())
                .Then(
                    globalRequestCacheKeys => globalRequestCacheKeys
                        .Select(p => new KeyValuePair<PlatformCacheKey, object>(p, null))
                        .Pipe(_ => new ConcurrentDictionary<PlatformCacheKey, object>(_)));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "LoadGlobalCachedKeys failed. Fallback to empty default value.");
            return new ConcurrentDictionary<PlatformCacheKey, object>();
        }
    }
}
