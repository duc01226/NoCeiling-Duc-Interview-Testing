using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Common.DependencyInjection;

public static class PlatformRegisterModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers a platform module of type 'TModule' into the services collection.
    /// </summary>
    public static IServiceCollection RegisterModule<TModule>(
        this IServiceCollection services) where TModule : PlatformModule
    {
        return RegisterModule(services, typeof(TModule));
    }

    /// <summary>
    /// Registers a platform module of type of 'moduleType' param into the services collection.
    /// </summary>
    public static IServiceCollection RegisterModule(
        this IServiceCollection services,
        Type moduleType)
    {
        if (!moduleType.IsAssignableTo(typeof(PlatformModule)))
            throw new ArgumentException("ModuleType parameter is invalid. It must be inherit from PlatformModule");

        services.Register(
            typeof(IServiceCollection),
            sp => services,
            ServiceLifeTime.Singleton,
            replaceIfExist: true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        RegisterModuleInstance(services, moduleType);

        var serviceProvider = services.BuildServiceProvider();

        var newRegisterModule = (PlatformModule)serviceProvider.GetRequiredService(moduleType);

        newRegisterModule.RegisterServices(services);

        serviceProvider
            .GetServices<PlatformModule>()
            .Where(p => !p.GetType().IsAssignableTo(moduleType))
            .ToList()
            .ForEach(
                otherRegisteredModule => otherRegisteredModule.OnNewOtherModuleRegistered(services, newRegisterModule));

        return services;
    }

    private static void RegisterModuleInstance(
        IServiceCollection services,
        Type moduleType)
    {
        services.Register(
            moduleType,
            moduleType,
            ServiceLifeTime.Singleton,
            replaceIfExist: false,
            skipIfExist: true);

        services.Register(
            typeof(PlatformModule),
            sp => sp.GetService(moduleType),
            ServiceLifeTime.Singleton,
            replaceIfExist: false);

        services.RegisterAllForImplementation(
            implementationType: moduleType,
            implementationFactory: sp => sp.GetService(moduleType),
            ServiceLifeTime.Singleton,
            replaceIfExist: false);
    }
}
