using System.Reflection;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Infrastructures.Caching.BuiltInCacheRepositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// Represents a module for caching in the platform infrastructure.
/// </summary>
/// <remarks>
/// This class is part of the Easy.Platform.Infrastructures.Caching namespace and extends the PlatformInfrastructureModule class.
/// It provides methods for registering and configuring caching services in the platform.
/// </remarks>
public class PlatformCachingModule : PlatformInfrastructureModule
{
    public PlatformCachingModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    /// <summary>
    /// Handles the event when a new module is registered in the platform.
    /// </summary>
    /// <param name="serviceCollection">The service collection where the new module is registered.</param>
    /// <param name="newOtherRegisterModule">The new module that has been registered.</param>
    /// <remarks>
    /// If the new module is not of type PlatformInfrastructureModule, this method will register cache items by scanning the assembly of the new module.
    /// </remarks>
    public override void OnNewOtherModuleRegistered(
        IServiceCollection serviceCollection,
        PlatformModule newOtherRegisterModule)
    {
        if (newOtherRegisterModule is not PlatformInfrastructureModule)
            RegisterCacheItemsByScanAssemblies(serviceCollection, newOtherRegisterModule.Assembly);
    }

    /// <summary>
    /// Provides a distributed cache repository for the platform.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <param name="configuration">The configuration to setup the distributed cache repository.</param>
    /// <returns>An instance of IPlatformDistributedCacheRepository, or null if no distributed cache is registered.</returns>
    /// <remarks>
    /// Override this method in a derived class to provide a custom implementation of IPlatformDistributedCacheRepository.
    /// The default implementation returns null, indicating that no distributed cache is registered.
    /// </remarks>
    protected virtual IPlatformDistributedCacheRepository DistributedCacheRepositoryProvider(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        return null;
    }

    /// <summary>
    /// Registers the services and configurations related to the platform caching module.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the services to.</param>
    /// <remarks>
    /// This method registers the platform cache repository provider, platform cache settings,
    /// default platform cache entry options, and cache items by scanning assemblies.
    /// It also registers the built-in default memory cache and distributed cache if available.
    /// Lastly, it registers a background service for automatically clearing deprecated global request cached keys.
    /// </remarks>
    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.Register<IPlatformCacheRepositoryProvider, PlatformCacheRepositoryProvider>(ServiceLifeTime.Singleton);
        serviceCollection.Register(
            typeof(PlatformCacheSettings),
            sp => new PlatformCacheSettings().With(settings => ConfigCacheSettings(sp, settings)));
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
        serviceCollection.Register(
            typeof(IPlatformCacheRepository),
            typeof(PlatformMemoryCacheRepository),
            ServiceLifeTime.Singleton);
        serviceCollection.RegisterAllForImplementation(typeof(PlatformCollectionMemoryCacheRepository<>));

        // Register Distributed Cache
        var tempCheckHasDistributedCacheInstance = DistributedCacheRepositoryProvider(ServiceProvider, Configuration);
        if (tempCheckHasDistributedCacheInstance != null)
        {
            tempCheckHasDistributedCacheInstance.Dispose();

            serviceCollection.Register(
                typeof(IPlatformCacheRepository),
                provider => DistributedCacheRepositoryProvider(provider, Configuration),
                ServiceLifeTime.Singleton);

            serviceCollection.RegisterAllForImplementation(typeof(PlatformCollectionDistributedCacheRepository<>));
        }

        serviceCollection.RegisterHostedService<PlatformAutoClearDeprecatedGlobalRequestCachedKeysBackgroundService>();
    }

    protected virtual void ConfigCacheSettings(IServiceProvider sp, PlatformCacheSettings cacheSettings)
    {
    }

    protected void RegisterDefaultPlatformCacheEntryOptions(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(
            sp => sp.GetRequiredService<PlatformCacheSettings>().DefaultCacheEntryOptions,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
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
