using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// Provides an interface for managing cache repositories in the platform.
/// </summary>
/// <remarks>
/// This interface provides methods for getting cache repositories, including the last registered repository,
/// a repository by type, and collection cache repositories. It also provides a method for trying to get a cache repository by type.
/// </remarks>
public interface IPlatformCacheRepositoryProvider
{
    /// <summary>
    /// Retrieves the last registered cache repository.
    /// </summary>
    /// <returns>The last registered cache repository.</returns>
    public IPlatformCacheRepository Get();

    /// <summary>
    /// Retrieves a cache repository by type.
    /// </summary>
    /// <param name="cacheRepositoryType">The type of the cache repository to retrieve.</param>
    /// <param name="fallbackMemoryCacheIfNotExist">Whether to fallback to memory cache if the specified cache repository does not exist.</param>
    /// <returns>The cache repository of the specified type, or memory cache if the specified cache repository does not exist and fallback is enabled.</returns>
    public IPlatformCacheRepository Get(PlatformCacheRepositoryType cacheRepositoryType, bool fallbackMemoryCacheIfNotExist = true);

    /// <summary>
    /// Tries to retrieve a cache repository by type.
    /// </summary>
    /// <param name="cacheRepositoryType">The type of the cache repository to retrieve.</param>
    /// <returns>The cache repository of the specified type, or null if it does not exist.</returns>
    public IPlatformCacheRepository TryGet(PlatformCacheRepositoryType cacheRepositoryType);

    /// <summary>
    /// Retrieves the last registered collection cache repository.
    /// </summary>
    /// <typeparam name="TCollectionCacheKeyProvider">The type of the collection cache key provider.</typeparam>
    /// <returns>The last registered collection cache repository.</returns>
    /// <remarks>
    /// The GetCollection[TCollectionCacheKeyProvider] method is a part of the IPlatformCacheRepositoryProvider interface and is implemented in the PlatformCacheRepositoryProvider class. This method is used to retrieve the last registered collection cache repository.
    /// <br />
    /// In the context of caching, a repository is a storage location where the data is stored and retrieved. In this case, the data is a collection, and the type of the collection is determined by the generic parameter TCollectionCacheKeyProvider.
    /// <br />
    /// The TCollectionCacheKeyProvider is a type that extends PlatformCollectionCacheKeyProvider, which is used to provide keys for the cache entries in the collection. This allows for efficient retrieval of data from the cache.
    /// <br />
    /// This method is used in various services to get a handle to the cache repository. This handle is then used to cache requests and responses, improving the performance of the system by reducing the need for expensive database or network calls.
    /// <br />
    /// In summary, the GetCollection[TCollectionCacheKeyProvider] method is a key part of the caching infrastructure, enabling efficient data storage and retrieval for various services in the system.
    /// </remarks>
    public IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider> GetCollection<TCollectionCacheKeyProvider>()
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider;

    /// <summary>
    /// Retrieves a collection cache repository by type.
    /// </summary>
    /// <typeparam name="TCollectionCacheKeyProvider">The type of the collection cache key provider.</typeparam>
    /// <param name="cacheRepositoryType">The type of the cache repository to retrieve.</param>
    /// <param name="fallbackMemoryCacheIfNotExist">Whether to fallback to memory cache if the specified cache repository does not exist.</param>
    /// <returns>The collection cache repository of the specified type, or memory cache if the specified cache repository does not exist and fallback is enabled.</returns>
    public IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
        GetCollection<TCollectionCacheKeyProvider>(PlatformCacheRepositoryType cacheRepositoryType, bool fallbackMemoryCacheIfNotExist = true)
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider;
}

/// <summary>
/// Provides a mechanism for managing cache repositories in the platform.
/// </summary>
/// <remarks>
/// This class is responsible for providing access to different types of cache repositories,
/// such as memory cache and distributed cache. It also allows for the retrieval of cache
/// repositories based on their type, and provides a fallback mechanism in case a specific
/// cache repository does not exist.
/// </remarks>
public class PlatformCacheRepositoryProvider : IPlatformCacheRepositoryProvider
{
    private readonly List<IPlatformCacheRepository> registeredCacheRepositories;
    private readonly Dictionary<PlatformCacheRepositoryType, IPlatformCacheRepository> registeredCacheRepositoriesDic;

    private readonly IServiceProvider serviceProvider;

    public PlatformCacheRepositoryProvider(
        IServiceProvider serviceProvider,
        IEnumerable<IPlatformCacheRepository> registeredCacheRepositories)
    {
        this.serviceProvider = serviceProvider;
        this.registeredCacheRepositories = registeredCacheRepositories.ToList();
        registeredCacheRepositoriesDic = BuildRegisteredCacheRepositoriesDic(this.registeredCacheRepositories);
    }

    public IPlatformCacheRepository Get()
    {
        return registeredCacheRepositories.Last();
    }

    public IPlatformCacheRepository Get(PlatformCacheRepositoryType cacheRepositoryType, bool fallbackMemoryCacheIfNotExist = true)
    {
        if (fallbackMemoryCacheIfNotExist == false)
            EnsureCacheRepositoryTypeRegistered(cacheRepositoryType);

        return registeredCacheRepositoriesDic.GetValueOrDefault(cacheRepositoryType) ?? registeredCacheRepositoriesDic[PlatformCacheRepositoryType.Memory];
    }

    public IPlatformCacheRepository TryGet(PlatformCacheRepositoryType cacheRepositoryType)
    {
        try
        {
            return Get(cacheRepositoryType);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider> GetCollection<TCollectionCacheKeyProvider>()
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
    {
        return serviceProvider
            .GetServices<IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>>()
            .Last();
    }

    public IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
        GetCollection<TCollectionCacheKeyProvider>(PlatformCacheRepositoryType cacheRepositoryType, bool fallbackMemoryCacheIfNotExist = true)
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider
    {
        if (fallbackMemoryCacheIfNotExist == false)
            EnsureCacheRepositoryTypeRegistered(cacheRepositoryType);

        return serviceProvider
                   .GetServices<IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>>()
                   .LastOrDefault(p => p.CacheRepositoryType() == cacheRepositoryType) ??
               serviceProvider
                   .GetServices<IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>>()
                   .LastOrDefault(p => p.CacheRepositoryType() == PlatformCacheRepositoryType.Memory);
    }

    private static Dictionary<PlatformCacheRepositoryType, IPlatformCacheRepository>
        BuildRegisteredCacheRepositoriesDic(List<IPlatformCacheRepository> registeredCacheRepositories)
    {
        return registeredCacheRepositories.GroupBy(p => p.GetType())
            .ToDictionary(
                p =>
                {
                    if (p.Key.IsAssignableTo(typeof(IPlatformDistributedCacheRepository)))
                        return PlatformCacheRepositoryType.Distributed;
                    if (p.Key.IsAssignableTo(typeof(IPlatformMemoryCacheRepository)))
                        return PlatformCacheRepositoryType.Memory;

                    throw new Exception($"Unknown PlatformCacheRepositoryType of {p.GetType().Name}");
                },
                p => p.Last());
    }

    private void EnsureCacheRepositoryTypeRegistered(PlatformCacheRepositoryType cacheRepositoryType)
    {
        if (!registeredCacheRepositoriesDic.ContainsKey(cacheRepositoryType))
            throw new Exception($"Type of {cacheRepositoryType} is not registered");
    }
}
