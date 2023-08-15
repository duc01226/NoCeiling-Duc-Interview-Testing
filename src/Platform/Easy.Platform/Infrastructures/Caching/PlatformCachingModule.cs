using System.Reflection;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Infrastructures.Caching.BuiltInCacheRepositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
