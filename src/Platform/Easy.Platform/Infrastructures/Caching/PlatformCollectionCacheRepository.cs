using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// Provides an interface for a platform collection cache repository.
/// </summary>
/// <typeparam name="TCollectionCacheKeyProvider">The type of the collection cache key provider.</typeparam>
/// <remarks>
/// This interface defines methods for getting, setting, and removing cache entries,
/// as well as methods for caching requests and removing all cache entries.
/// </remarks>
public interface IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
    where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
{
    T Get<T>(string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey);

    /// <summary>
    /// Retrieves the cached value of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <param name="requestKeyParts">An array of strings that form the request key. If null, the default request key is used.</param>
    /// <returns>The cached value of the specified type.</returns>
    /// <remarks>
    /// This method retrieves the cached value associated with the request key formed by the provided array of strings.
    /// If the array is null, the default request key is used.
    /// </remarks>
    T Get<T>(string[] requestKeyParts = null);

    Task<T> GetAsync<T>(
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default);

    Task<T> GetAsync<T>(string[] requestKeyParts = null, CancellationToken token = default);

    void Set<T>(
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey);

    void Set<T>(T value, PlatformCacheEntryOptions cacheOptions = null, string[] requestKeyParts = null);

    Task SetAsync<T>(
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default);

    Task SetAsync<T>(
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        string[] requestKeyParts = null,
        CancellationToken token = default);

    void Set<T>(
        T value,
        double? absoluteExpirationInSeconds = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey);

    void Set<T>(T value, double? absoluteExpirationInSeconds = null, string[] requestKeyParts = null);

    Task SetAsync<T>(
        T value,
        double? absoluteExpirationInSeconds = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default);

    Task SetAsync<T>(
        T value,
        double? absoluteExpirationInSeconds = null,
        string[] requestKeyParts = null,
        CancellationToken token = default);

    Task RemoveAsync(
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default);

    Task RemoveAsync(string[] requestKeyParts = null, CancellationToken token = default);

    PlatformCacheRepositoryType CacheRepositoryType();

    Task RemoveAllAsync();

    /// <summary>
    /// Asynchronously removes the cache entries that match the specified predicate.
    /// </summary>
    /// <param name="cacheRequestKeyPredicate">The function to test each cache request key for a condition.</param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// The method will remove all cache entries where the cache request key satisfies the condition provided by the predicate function.
    /// </remarks>
    Task RemoveAsync(
        Func<string, bool> cacheRequestKeyPredicate,
        CancellationToken token = default);

    Task RemoveAsync(
        Func<string[], bool> cacheRequestKeyPartsPredicate,
        CancellationToken token = default);

    Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default);

    Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string[] requestKeyParts = null,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default);

    Task<TData> CacheRequestUseConfigOptionsAsync<TConfigurationCacheOptions, TData>(
        Func<Task<TData>> request,
        string[] requestKeyParts = null,
        CancellationToken token = default)
        where TConfigurationCacheOptions : PlatformConfigurationCacheEntryOptions;

    Task<TData> CacheRequestUseConfigOptionsAsync<TConfigurationCacheOptions, TData>(
        Func<Task<TData>> request,
        string requestKey,
        CancellationToken token = default)
        where TConfigurationCacheOptions : PlatformConfigurationCacheEntryOptions;

    Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string requestKey,
        double? absoluteExpirationInSeconds,
        CancellationToken token = default);

    Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string[] requestKeyParts,
        double? absoluteExpirationInSeconds,
        CancellationToken token = default);
}

/// <summary>
/// Collection cache repository for last registered cache repository
/// </summary>
public abstract class PlatformCollectionCacheRepository<TCollectionCacheKeyProvider> : IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
    where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
{
    protected readonly IPlatformCacheRepositoryProvider CacheRepositoryProvider;
    protected readonly TCollectionCacheKeyProvider CollectionCacheKeyProvider;
    protected readonly IServiceProvider ServiceProvider;

    public PlatformCollectionCacheRepository(
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        TCollectionCacheKeyProvider collectionCacheKeyProvider,
        IServiceProvider serviceProvider)
    {
        CollectionCacheKeyProvider = collectionCacheKeyProvider;
        ServiceProvider = serviceProvider;
        CacheRepositoryProvider = cacheRepositoryProvider;
    }

    public T Get<T>(string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey)
    {
        return CacheRepository().Get<T>(CollectionCacheKeyProvider.GetKey(requestKey));
    }

    public T Get<T>(string[] requestKeyParts = null)
    {
        return CacheRepository().Get<T>(CollectionCacheKeyProvider.GetKey(requestKeyParts));
    }

    public Task<T> GetAsync<T>(
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default)
    {
        return CacheRepository().GetAsync<T>(CollectionCacheKeyProvider.GetKey(requestKey), token);
    }

    public Task<T> GetAsync<T>(string[] requestKeyParts = null, CancellationToken token = default)
    {
        return CacheRepository().GetAsync<T>(CollectionCacheKeyProvider.GetKey(requestKeyParts), token);
    }

    public void Set<T>(
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey)
    {
        CacheRepository().Set(CollectionCacheKeyProvider.GetKey(requestKey), value, cacheOptions);
    }

    public void Set<T>(T value, PlatformCacheEntryOptions cacheOptions = null, string[] requestKeyParts = null)
    {
        CacheRepository().Set(CollectionCacheKeyProvider.GetKey(requestKeyParts), value, cacheOptions);
    }

    public async Task SetAsync<T>(
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default)
    {
        await CacheRepository()
            .SetAsync(
                CollectionCacheKeyProvider.GetKey(requestKey),
                value,
                cacheOptions,
                token);
    }

    public async Task SetAsync<T>(
        T value,
        PlatformCacheEntryOptions cacheOptions = null,
        string[] requestKeyParts = null,
        CancellationToken token = default)
    {
        await CacheRepository()
            .SetAsync(
                CollectionCacheKeyProvider.GetKey(requestKeyParts),
                value,
                cacheOptions,
                token);
    }

    public void Set<T>(
        T value,
        double? absoluteExpirationInSeconds = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey)
    {
        var defaultCacheOptions = CacheRepository()
            .GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        Set(value, defaultCacheOptions, requestKey);
    }

    public void Set<T>(T value, double? absoluteExpirationInSeconds = null, string[] requestKeyParts = null)
    {
        var defaultCacheOptions = CacheRepository()
            .GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        Set(value, defaultCacheOptions, requestKeyParts);
    }

    public async Task SetAsync<T>(
        T value,
        double? absoluteExpirationInSeconds = null,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default)
    {
        var defaultCacheOptions = CacheRepository()
            .GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        await SetAsync(
            value,
            defaultCacheOptions,
            requestKey,
            token);
    }

    public async Task SetAsync<T>(
        T value,
        double? absoluteExpirationInSeconds = null,
        string[] requestKeyParts = null,
        CancellationToken token = default)
    {
        var defaultCacheOptions = CacheRepository()
            .GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        await SetAsync(
            value,
            defaultCacheOptions,
            requestKeyParts,
            token);
    }

    public async Task RemoveAsync(
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        CancellationToken token = default)
    {
        await CacheRepository().RemoveAsync(CollectionCacheKeyProvider.GetKey(requestKey), token);
    }

    public async Task RemoveAsync(string[] requestKeyParts = null, CancellationToken token = default)
    {
        await CacheRepository().RemoveAsync(CollectionCacheKeyProvider.GetKey(requestKeyParts), token);
    }

    public async Task RemoveAllAsync()
    {
        await RemoveAsync((Func<string, bool>)(p => true));
    }

    public async Task RemoveAsync(
        Func<string, bool> cacheRequestKeyPredicate,
        CancellationToken token = default)
    {
        var matchCollectionKeyPredicate = CollectionCacheKeyProvider.MatchCollectionKeyPredicate();

        await CacheRepository()
            .RemoveAsync(
                cacheKey => matchCollectionKeyPredicate(cacheKey) && cacheRequestKeyPredicate(cacheKey.RequestKey),
                token);
    }

    public async Task RemoveAsync(
        Func<string[], bool> cacheRequestKeyPartsPredicate,
        CancellationToken token = default)
    {
        var matchCollectionKeyPredicate = CollectionCacheKeyProvider.MatchCollectionKeyPredicate();

        await CacheRepository()
            .RemoveAsync(
                cacheKey => matchCollectionKeyPredicate(cacheKey) && cacheRequestKeyPartsPredicate(cacheKey.RequestKeyParts()),
                token);
    }

    public Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        return CacheRepository()
            .CacheRequestAsync(
                request,
                CollectionCacheKeyProvider.GetKey(requestKey),
                cacheOptions,
                token);
    }

    public Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string[] requestKeyParts = null,
        PlatformCacheEntryOptions cacheOptions = null,
        CancellationToken token = default)
    {
        return CacheRepository()
            .CacheRequestAsync(
                request,
                CollectionCacheKeyProvider.GetKey(requestKeyParts),
                cacheOptions,
                token);
    }

    public Task<TData> CacheRequestUseConfigOptionsAsync<TConfigurationCacheOptions, TData>(
        Func<Task<TData>> request,
        string[] requestKeyParts = null,
        CancellationToken token = default)
        where TConfigurationCacheOptions : PlatformConfigurationCacheEntryOptions
    {
        return CacheRequestAsync(
            request,
            requestKeyParts,
            ServiceProvider.GetService<TConfigurationCacheOptions>(),
            token);
    }

    public Task<TData> CacheRequestUseConfigOptionsAsync<TConfigurationCacheOptions, TData>(
        Func<Task<TData>> request,
        string requestKey,
        CancellationToken token = default)
        where TConfigurationCacheOptions : PlatformConfigurationCacheEntryOptions
    {
        return CacheRequestAsync(
            request,
            requestKey,
            ServiceProvider.GetService<TConfigurationCacheOptions>(),
            token);
    }

    public Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string requestKey,
        double? absoluteExpirationInSeconds,
        CancellationToken token = default)
    {
        var defaultCacheOptions = CacheRepository()
            .GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        return CacheRequestAsync(
            request,
            requestKey,
            defaultCacheOptions,
            token);
    }

    public Task<TData> CacheRequestAsync<TData>(
        Func<Task<TData>> request,
        string[] requestKeyParts,
        double? absoluteExpirationInSeconds,
        CancellationToken token = default)
    {
        var defaultCacheOptions = CacheRepository()
            .GetDefaultCacheEntryOptions()
            .WithOptionalCustomAbsoluteExpirationInSeconds(absoluteExpirationInSeconds);

        return CacheRequestAsync(
            request,
            requestKeyParts,
            defaultCacheOptions,
            token);
    }

    public abstract PlatformCacheRepositoryType CacheRepositoryType();

    protected IPlatformCacheRepository CacheRepository()
    {
        return CacheRepositoryProvider.Get(CacheRepositoryType());
    }
}

public class PlatformCollectionMemoryCacheRepository<TCollectionCacheKeyProvider> : PlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
    where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
{
    public PlatformCollectionMemoryCacheRepository(
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        TCollectionCacheKeyProvider collectionCacheKeyProvider,
        IServiceProvider serviceProvider) : base(
        cacheRepositoryProvider,
        collectionCacheKeyProvider,
        serviceProvider)
    {
    }

    public override PlatformCacheRepositoryType CacheRepositoryType()
    {
        return PlatformCacheRepositoryType.Memory;
    }
}

public class PlatformCollectionDistributedCacheRepository<TCollectionCacheKeyProvider> : PlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
    where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
{
    public PlatformCollectionDistributedCacheRepository(
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        TCollectionCacheKeyProvider collectionCacheKeyProvider,
        IServiceProvider serviceProvider) : base(
        cacheRepositoryProvider,
        collectionCacheKeyProvider,
        serviceProvider)
    {
    }

    public override PlatformCacheRepositoryType CacheRepositoryType()
    {
        return PlatformCacheRepositoryType.Distributed;
    }
}
