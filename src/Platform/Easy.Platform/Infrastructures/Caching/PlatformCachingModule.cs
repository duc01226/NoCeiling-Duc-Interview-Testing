using System.Reflection;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Infrastructures.Caching.BuiltInCacheRepositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.Caching;

public class PlatformCachingModule : PlatformInfrastructureModule
{
    public PlatformCachingModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public override void OnNewOtherModuleRegistered(
        IServiceCollection serviceCollection,
        PlatformModule newOtherRegisterModule)
    {
        if (newOtherRegisterModule is not PlatformInfrastructureModule)
            RegisterCacheItemsByScanAssemblies(serviceCollection, newOtherRegisterModule.Assembly);
    }

    /// <summary>
    /// Override this function provider to register IPlatformDistributedCache. Default return null;
    /// </summary>
    protected virtual IPlatformDistributedCacheRepository DistributedCacheRepositoryProvider(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        return null;
    }

    /// <summary>
    /// Override this function provider to register IPlatformMemoryCacheRepository. Default return PlatformMemoryCacheRepository;
    /// </summary>
    protected virtual IPlatformMemoryCacheRepository MemoryCacheRepositoryProvider(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        return new PlatformMemoryCacheRepository(serviceProvider.GetService<ILoggerFactory>(), serviceProvider);
    }

    /// <summary>
    /// Override this method to config default PlatformCacheEntryOptions when save cache
    /// </summary>
    protected virtual PlatformCacheEntryOptions DefaultPlatformCacheEntryOptions(IServiceProvider serviceProvider)
    {
        return null;
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.Register<IPlatformCacheRepositoryProvider, PlatformCacheRepositoryProvider>();
        RegisterDefaultPlatformCacheEntryOptions(serviceCollection);

        RegisterCacheItemsByScanAssemblies(
            serviceCollection,
            assemblies: Util.ListBuilder.New(Assembly)
                .Concat(
                    ServiceProvider.GetServices<PlatformModule>()
                        .Where(p => p is not PlatformInfrastructureModule)
                        .Select(p => p.GetType().Assembly))
                .Distinct()
                .ToArray());

        // Register built-in default memory cache
        serviceCollection.RegisterAllForImplementation(
            provider => MemoryCacheRepositoryProvider(provider, Configuration),
            ServiceLifeTime.Singleton);
        serviceCollection.RegisterAllForImplementation(typeof(PlatformCollectionMemoryCacheRepository<>));

        // Register Distributed Cache
        var tempCheckHasDistributedCacheInstance = DistributedCacheRepositoryProvider(ServiceProvider, Configuration);
        if (tempCheckHasDistributedCacheInstance != null)
        {
            tempCheckHasDistributedCacheInstance.Dispose();

            serviceCollection.RegisterAllForImplementation(
                provider => DistributedCacheRepositoryProvider(provider, Configuration),
                ServiceLifeTime.Singleton);

            serviceCollection.RegisterAllForImplementation(typeof(PlatformCollectionDistributedCacheRepository<>));
        }
    }

    protected void RegisterDefaultPlatformCacheEntryOptions(IServiceCollection serviceCollection)
    {
        if (DefaultPlatformCacheEntryOptions(ServiceProvider) != null)
            serviceCollection.Register(
                typeof(PlatformCacheEntryOptions),
                DefaultPlatformCacheEntryOptions,
                ServiceLifeTime.Transient,
                replaceIfExist: true,
                DependencyInjectionExtension.ReplaceServiceStrategy.ByService);
        else if (serviceCollection.All(p => p.ServiceType != typeof(PlatformCacheEntryOptions)))
            serviceCollection.Register<PlatformCacheEntryOptions>();
    }

    protected void RegisterCacheItemsByScanAssemblies(
        IServiceCollection serviceCollection,
        params Assembly[] assemblies)
    {
        assemblies.ForEach(
            cacheItemsScanAssembly =>
            {
                serviceCollection.RegisterAllFromType<IPlatformContextCacheKeyProvider>(cacheItemsScanAssembly);
                serviceCollection.RegisterAllFromType<PlatformConfigurationCacheEntryOptions>(cacheItemsScanAssembly);
            });
    }
}
