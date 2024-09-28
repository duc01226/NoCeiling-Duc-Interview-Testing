using System.Collections.Concurrent;
using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// The IPlatformCacheRepository interface in the Easy.Platform.Infrastructures.Caching namespace is a key component of the caching infrastructure in the application. It provides a unified way to interact with different types of caching mechanisms, such as in-memory and distributed caches.
/// <br />
/// This interface defines methods for common caching operations, such as getting, setting, and removing cache entries. It also provides methods for handling asynchronous operations and managing cache entry options, including expiration settings.
/// <br />
/// The PlatformCacheRepository abstract class implements this interface, and specific cache repository classes like PlatformRedisDistributedCacheRepository and PlatformMemoryCacheRepository extend this abstract class to provide concrete implementations for different caching mechanisms.
/// <br />
/// The IPlatformMemoryCacheRepository and IPlatformDistributedCacheRepository interfaces extend IPlatformCacheRepository, indicating that they share the same basic caching operations but may have additional features specific to memory or distributed caching.
/// <br />
/// Overall, the IPlatformCacheRepository interface is crucial for abstracting the underlying caching mechanism, allowing the rest of the application to interact with the cache in a consistent and technology-agnostic manner.
/// </summary>
public interface IPlatformCacheRepository
{
    public const string DefaultGlobalContext = "__DefaultGlobalCacheContext__";
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformCacheRepository)}");

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
    /// Return default cache entry options value. This could be config when register module, override <see cref="PlatformCachingModule.ConfigCacheSettings" />
    /// </summary>
    PlatformCacheEntryOptions GetDefaultCacheEntryOptions();

    /// The ProcessClearDeprecatedGlobalRequestCachedKeys method is part of the IPlatformCacheRepository interface and is implemented in the PlatformCacheRepository abstract class. This method is designed to clear deprecated or outdated keys from the global request cache.
    /// <br />
    /// In the context of a caching system, this method is crucial for maintaining the freshness and relevance of the data stored in the cache. Over time, certain keys in the cache may become outdated or irrelevant, and keeping these keys can lead to inefficient use of memory and potentially incorrect data being served to the client.
    /// <br />
    /// The method is implemented in both PlatformMemoryCacheRepository and PlatformRedisDistributedCacheRepository classes, indicating that it's used for both in-memory and distributed Redis cache repositories.
    /// <br />
    /// In the PlatformAutoClearDeprecatedGlobalRequestCachedKeysBackgroundService class, this method is called in an interval process, suggesting that the clearing of deprecated global request cache keys is performed regularly as a background task. This helps to ensure that the cache is consistently maintained and that outdated keys are removed on a regular basis.
    Task ProcessClearDeprecatedGlobalRequestCachedKeys();
}

public abstract class PlatformCacheRepository : IPlatformCacheRepository
{
    public static readonly string CachedKeysCollectionName = "___PlatformGlobalCacheKeys___";

    protected readonly PlatformCacheSettings CacheSettings;

    protected readonly IServiceProvider ServiceProvider;

    private readonly Lazy<ILogger> loggerLazy;

    public PlatformCacheRepository(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        PlatformCacheSettings cacheSettings)
    {
        ServiceProvider = serviceProvider;
        loggerLazy = new Lazy<ILogger>(
            () => loggerFactory.CreateLogger(typeof(PlatformCacheRepository).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}"));
        CacheSettings = cacheSettings;
    }

    protected ILogger Logger => loggerLazy.Value;

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
            ServiceProvider.GetService<TCollectionCacheKeyProvider>()!.MatchCollectionKeyPredicate(),
            token);
    }

    public async Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        PlatformCacheKey cacheKey,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        var cachedDataResult = await TryGetAsync<TData>(cacheKey, token);

        return cachedDataResult.IsValid && cachedDataResult.Value != null ? cachedDataResult.Value : await RequestAndCacheNewData();

        async Task<TData> RequestAndCacheNewData()
        {
            var requestedData = await request();

            Util.TaskRunner.QueueActionInBackground(
                async () =>
                {
                    await TrySetAsync(
                        cacheKey,
                        requestedData,
                        cacheOptions,
                        token);
                },
                () => Logger,
                cancellationToken: token,
                logFullStackTraceBeforeBackgroundTask: false);

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
        return ServiceProvider.GetService<PlatformCacheEntryOptions>() ?? CacheSettings.DefaultCacheEntryOptions;
    }

    public abstract Task ProcessClearDeprecatedGlobalRequestCachedKeys();

    protected async Task<PlatformValidationResult<T>> TryGetAsync<T>(PlatformCacheKey cacheKey, CancellationToken token = default)
    {
        try
        {
            return await GetAsync<T>(cacheKey, token).Then(data => PlatformValidationResult<T>.Valid(data));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.BeautifyStackTrace(), "Try get data from cache failed. CacheKey:{CacheKey}", cacheKey.ToString());

            return PlatformValidationResult<T>.Invalid(default, ex.Message);
        }
    }

    protected async Task TrySetAsync<T>(
        PlatformCacheKey cacheKey,
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        try
        {
            await SetAsync(cacheKey, value, cacheOptions, token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.BeautifyStackTrace(), "Try set data to cache failed. CacheKey:{CacheKey}", cacheKey.ToString());
        }
    }

    /// <summary>
    /// Used to build a unique cache key to store list of all request cached keys
    /// </summary>
    public abstract PlatformCacheKey GetGlobalAllRequestCachedKeysCacheKey();

    protected async Task<ConcurrentDictionary<PlatformCacheKey, object>> LoadGlobalAllRequestCachedKeys()
    {
        try
        {
            return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    return await GetAsync<List<PlatformCacheKey>>(cacheKey: GetGlobalAllRequestCachedKeysCacheKey())
                        .Then(keys => keys ?? [])
                        .Then(
                            globalRequestCacheKeys => globalRequestCacheKeys
                                .Select(p => new KeyValuePair<PlatformCacheKey, object>(p, null))
                                .Pipe(items => new ConcurrentDictionary<PlatformCacheKey, object>(items)));
                });
        }
        catch (Exception e)
        {
            Logger.LogError(e.BeautifyStackTrace(), "LoadGlobalCachedKeys failed. Fallback to empty default value.");

            return new ConcurrentDictionary<PlatformCacheKey, object>();
        }
    }

    protected DistributedCacheEntryOptions MapToDistributedCacheEntryOptions(PlatformCacheEntryOptions options)
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = options?.AbsoluteExpirationRelativeToNow() ?? CacheSettings.DefaultCacheEntryOptions.AbsoluteExpirationRelativeToNow(),
            SlidingExpiration = options?.SlidingExpiration() ?? CacheSettings.DefaultCacheEntryOptions.SlidingExpiration()
        };
    }
}
