using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// Cache Provider provide cache repository like MemoryCache, DistributedCache.
/// </summary>
public interface IPlatformCacheRepositoryProvider
{
    /// <summary>
    /// Get last registered cache repository
    /// </summary>
    public IPlatformCacheRepository Get();

    /// <summary>
    /// Get cache repository by type
    /// </summary>
    public IPlatformCacheRepository Get(PlatformCacheRepositoryType cacheRepositoryType, bool fallbackMemoryCacheIfNotExist = true);

    /// <summary>
    /// Try Get cache repository by type. Return null if not existed
    /// </summary>
    public IPlatformCacheRepository TryGet(PlatformCacheRepositoryType cacheRepositoryType);

    /// <summary>
    /// Get last registered collection cache repository
    /// </summary>
    public IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider> GetCollection<TCollectionCacheKeyProvider>()
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider;

    /// <summary>
    /// Get collection cache repository by type
    /// </summary>
    public IPlatformCollectionCacheRepository<TCollectionCacheKeyProvider>
        GetCollection<TCollectionCacheKeyProvider>(PlatformCacheRepositoryType cacheRepositoryType, bool fallbackMemoryCacheIfNotExist = true)
        where TCollectionCacheKeyProvider : PlatformCollectionCacheKeyProvider;
}

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
