using System.Collections.Concurrent;
using System.Reflection;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Easy.Platform.Common.Extensions;

public static class DependencyInjectionExtension
{
    private static readonly Dictionary<string, Func<IServiceProvider, object>> RegisteredHostedServiceImplementTypeToImplementFactoryDict = [];
    private static readonly ConcurrentDictionary<string, object> RegisterHostedServiceLockDict = new();

    public static string[] DefaultIgnoreRegisterLibraryInterfacesNameSpacePrefixes { get; set; } = ["System", "Microsoft"];

    /// <summary>
    /// Registers all types from a specified assembly that are assignable to a given type.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="conventionalType">The type to which the types to be registered must be assignable.</param>
    /// <param name="assembly">The assembly to scan for types to register.</param>
    /// <param name="lifeTime">The lifetime of the services that are registered.</param>
    /// <param name="replaceIfExist">A flag indicating whether to replace existing registrations.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service is already registered.</param>
    /// <param name="skipIfExist">A flag indicating whether to skip registration if the service already exists.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when checking if a service should be skipped.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterAllFromType(
        this IServiceCollection services,
        Type conventionalType,
        Assembly assembly,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        assembly.GetTypes()
            .Where(
                implementationType => implementationType.IsClass &&
                                      !implementationType.IsAbstract &&
                                      (implementationType.IsAssignableTo(conventionalType) ||
                                       (conventionalType!.IsGenericType &&
                                        implementationType.IsGenericType &&
                                        implementationType.IsAssignableToGenericType(conventionalType))))
            .ToList()
            .ForEach(
                implementationType =>
                {
                    services.Register(
                        implementationType,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy: replaceStrategy,
                        skipIfExist: skipIfExist,
                        skipIfExistStrategy: skipIfExistStrategy);

                    services.RegisterInterfacesForImplementation(
                        implementationType,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy,
                        skipIfExist: skipIfExist,
                        skipIfExistStrategy: skipIfExistStrategy);
                });

        return services;
    }

    /// <summary>
    /// Registers all concrete types in an assembly that are assignable to a given conventional type.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="conventionalType">The type to which the concrete types should be assignable.</param>
    /// <param name="assembly">The assembly to search for types.</param>
    /// <param name="lifeTime">The lifetime of the services. Default is Transient.</param>
    /// <param name="replaceIfExist">Indicates whether to replace the service if it already exists. Default is true.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service should be replaced. Default is ByBoth.</param>
    /// <param name="skipIfExist">Indicates whether to skip the service if it already exists. Default is false.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when checking if a service should be skipped. Default is ByBoth.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterAllSelfImplementationFromType(
        this IServiceCollection services,
        Type conventionalType,
        Assembly assembly,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        assembly.GetTypes()
            .Where(
                implementationType => implementationType.IsClass &&
                                      !implementationType.IsAbstract &&
                                      (implementationType.IsAssignableTo(conventionalType) ||
                                       (conventionalType!.IsGenericType &&
                                        implementationType.IsGenericType &&
                                        implementationType.IsAssignableToGenericType(conventionalType))))
            .ToList()
            .ForEach(
                implementationType =>
                {
                    services.Register(
                        implementationType,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy: replaceStrategy,
                        skipIfExist: skipIfExist,
                        skipIfExistStrategy: skipIfExistStrategy);
                });

        return services;
    }

    /// <summary>
    /// Registers all concrete types in the specified assembly that are assignable to the type parameter TConventional.
    /// </summary>
    /// <typeparam name="TConventional">The type that the concrete types should be assignable to.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly to search for types to add.</param>
    /// <param name="lifeTime">The lifetime of the services being registered.</param>
    /// <param name="replaceIfExist">Whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing existing services.</param>
    /// <param name="skipIfExist">Whether to skip the service if it already exists.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when skipping existing services.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterAllFromType<TConventional>(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return RegisterAllFromType(
            services,
            typeof(TConventional),
            assembly,
            lifeTime,
            replaceIfExist,
            replaceStrategy,
            skipIfExist: skipIfExist,
            skipIfExistStrategy: skipIfExistStrategy);
    }

    /// <summary>
    /// Registers all concrete types in the specified assembly that are assignable to the given conventional type as themselves.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="conventionalType">The type to which the concrete types should be assignable.</param>
    /// <param name="assembly">The assembly to scan for matching types.</param>
    /// <param name="lifeTime">The lifetime of the services. Default is Transient.</param>
    /// <param name="replaceIfExist">Whether to replace the service if it already exists. Default is true.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service should be replaced. Default is ByBoth.</param>
    /// <param name="skipIfExist">Whether to skip the service if it already exists. Default is false.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when checking if a service should be skipped. Default is ByBoth.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterAllSelfImplementationFromType<TConventional>(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return RegisterAllSelfImplementationFromType(
            services,
            typeof(TConventional),
            assembly,
            lifeTime,
            replaceIfExist,
            replaceStrategy,
            skipIfExist: skipIfExist,
            skipIfExistStrategy: skipIfExistStrategy);
    }

    /// <summary>
    /// Registers the specified implementation type as itself and for all of its implemented interfaces.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="implementationType">The type of the implementation to register.</param>
    /// <param name="lifeTime">The <see cref="ServiceLifeTime" /> of the service.</param>
    /// <param name="replaceIfExist">If set to true, existing registrations for the same service type will be replaced.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing existing registrations.</param>
    /// <param name="skipIfExist">If set to true, the registration will be skipped if there is an existing registration for the same service type.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when skipping existing registrations.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterAllForImplementation(
        this IServiceCollection services,
        Type implementationType,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        services.RegisterIfNotExist(implementationType, implementationType, lifeTime, skipIfExistStrategy);

        services.RegisterInterfacesForImplementation(
            implementationType,
            lifeTime,
            replaceIfExist,
            replaceStrategy,
            skipIfExist: skipIfExist,
            skipIfExistStrategy: skipIfExistStrategy);

        return services;
    }

    /// <summary>
    /// Registers a type and its implemented interfaces in the service collection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="implementationType">The type of the service to register.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <param name="lifeTime">The lifetime of the service.</param>
    /// <param name="replaceIfExist">Indicates whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing the service.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    public static IServiceCollection RegisterAllForImplementation(
        this IServiceCollection services,
        Type implementationType,
        Func<IServiceProvider, object> implementationFactory,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        services.RegisterIfNotExist(implementationType, implementationType, lifeTime, CheckRegisteredStrategy.ByBoth);

        services.RegisterInterfacesForImplementation(
            implementationType,
            implementationFactory,
            lifeTime,
            replaceIfExist,
            replaceStrategy);

        return services;
    }

    /// <summary>
    /// Registers all services that implement the specified type in the <see cref="IServiceCollection" />.
    /// </summary>
    /// <typeparam name="TImplementation">The type of the implementation to register.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="lifeTime">The <see cref="ServiceLifeTime" /> to use when registering the service.</param>
    /// <param name="replaceIfExist">A boolean value indicating whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service is registered.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterAllForImplementation<TImplementation>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return RegisterAllForImplementation(
            services,
            typeof(TImplementation),
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Registers an instance of type TImplementation and all its implemented interfaces in the service collection.
    /// </summary>
    /// <typeparam name="TImplementation">The type of the implementation to register.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="implementationFactory">A factory that creates instances of the service type.</param>
    /// <param name="lifeTime">The lifetime of the service in the collection.</param>
    /// <param name="replaceIfExist">A boolean value to determine if existing registrations should be replaced.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    public static IServiceCollection RegisterAllForImplementation<TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> implementationFactory,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true)
    {
        services.Register(
            typeof(TImplementation),
            implementationFactory,
            lifeTime,
            replaceIfExist);

        services.RegisterInterfacesForImplementation(implementationFactory, lifeTime, replaceIfExist);

        return services;
    }

    /// <summary>
    /// Registers a service of the specified type with an implementation type in the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The implementation type of the service.</param>
    /// <param name="lifeTime">The lifetime of the service. Default is <see cref="ServiceLifeTime.Transient" />.</param>
    /// <param name="replaceIfExist">Indicates whether to replace the service if it already exists. Default is true.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing an existing service. Default is <see cref="CheckRegisteredStrategy.ByBoth" />.</param>
    /// <param name="skipIfExist">Indicates whether to skip registration if the service already exists. Default is false.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when skipping an existing service. Default is <see cref="CheckRegisteredStrategy.ByBoth" />.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection Register(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        if (skipIfExist)
        {
            if (skipIfExistStrategy == CheckRegisteredStrategy.ByBoth &&
                services.Any(p => p.ServiceType == serviceType && p.ImplementationType == implementationType)) return services;
            if (skipIfExistStrategy == CheckRegisteredStrategy.ByService &&
                services.Any(p => p.ServiceType == serviceType)) return services;
            if (skipIfExistStrategy == CheckRegisteredStrategy.ByImplementation &&
                services.Any(p => p.ImplementationType == implementationType)) return services;
        }

        switch (lifeTime)
        {
            case ServiceLifeTime.Scoped:
                if (replaceIfExist)
                    services.ReplaceScoped(serviceType, implementationType, replaceStrategy);
                else
                    services.AddScoped(serviceType, implementationType);
                break;
            case ServiceLifeTime.Singleton:
                if (replaceIfExist)
                    services.ReplaceSingleton(serviceType, implementationType, replaceStrategy);
                else
                    services.AddSingleton(serviceType, implementationType);
                break;

            default:
                if (replaceIfExist)
                    services.ReplaceTransient(serviceType, implementationType, replaceStrategy);
                else
                    services.AddTransient(serviceType, implementationType);
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers a service of type TService with an implementation of type TImplementation in the provided IServiceCollection.
    /// </summary>
    /// <typeparam name="TService">The type of the service to register.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation to use for the service.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="lifeTime">The lifetime of the service. Default is Transient.</param>
    /// <param name="replaceIfExist">If set to true, replaces the existing service registration if it exists. Default is true.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service registration should be replaced. Default is ByBoth.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection Register<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return Register(
            services,
            typeof(TService),
            typeof(TImplementation),
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Registers a service of type TService with an implementation of type TImplementation in the specified IServiceCollection if the service does not already exist.
    /// </summary>
    /// <typeparam name="TService">The type of the service to add.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="lifeTime">The lifetime of the service.</param>
    /// <param name="checkExistingStrategy">The strategy to use when checking if the service already exists.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterIfNotExist<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime,
        CheckRegisteredStrategy checkExistingStrategy)
    {
        return RegisterIfNotExist(
            services,
            typeof(TService),
            typeof(TImplementation),
            lifeTime,
            checkExistingStrategy);
    }

    /// <summary>
    /// Registers the specified service type and implementation type in the service collection if they do not already exist.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The type of the implementation to use.</param>
    /// <param name="lifeTime">The lifetime of the service.</param>
    /// <param name="checkExistingStrategy">The strategy to use when checking if the service already exists.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterIfNotExist(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifeTime lifeTime,
        CheckRegisteredStrategy checkExistingStrategy)
    {
        if (checkExistingStrategy == CheckRegisteredStrategy.ByBoth &&
            services.Any(p => p.ServiceType == serviceType && p.ImplementationType == implementationType)) return services;
        if (checkExistingStrategy == CheckRegisteredStrategy.ByService &&
            services.Any(p => p.ServiceType == serviceType)) return services;
        if (checkExistingStrategy == CheckRegisteredStrategy.ByImplementation &&
            services.Any(p => p.ImplementationType == implementationType)) return services;

        return Register(
            services,
            serviceType,
            implementationType,
            lifeTime);
    }

    /// <summary>
    /// Registers the specified service and implementation in the IServiceCollection if the service does not already exist.
    /// </summary>
    /// <typeparam name="TService">The type of the service to add.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="lifeTime">The lifetime of the service. Default is Transient.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterIfServiceNotExist<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient)
    {
        return RegisterIfServiceNotExist(
            services,
            typeof(TService),
            typeof(TImplementation),
            lifeTime);
    }

    /// <summary>
    /// Registers the specified service type and implementation type in the service collection if the service does not already exist.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The implementation type of the service to register.</param>
    /// <param name="lifeTime">The <see cref="ServiceLifeTime" /> of the service (default is <see cref="ServiceLifeTime.Transient" />).</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterIfServiceNotExist(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient)
    {
        if (services.Any(p => p.ServiceType == serviceType)) return services;

        return Register(
            services,
            serviceType,
            implementationType,
            lifeTime);
    }

    /// <summary>
    /// Registers a service in the IServiceCollection if it does not already exist.
    /// </summary>
    /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationProvider">A factory that creates the service.</param>
    /// <param name="lifeTime">The lifetime of the service in the IServiceCollection.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterIfServiceNotExist<TImplementation>(
        this IServiceCollection services,
        Type serviceType,
        Func<IServiceProvider, TImplementation> implementationProvider,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient)
    {
        if (services.Any(p => p.ServiceType == serviceType)) return services;

        return Register(
            services,
            serviceType,
            implementationProvider,
            lifeTime);
    }

    /// <summary>
    /// Registers a service of the specified type in the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="implementationType">The type of the service to register.</param>
    /// <param name="lifeTime">The <see cref="ServiceLifeTime" /> of the service (default is Transient).</param>
    /// <param name="replaceIfExist">A boolean value indicating whether to replace the service if it already exists (default is true).</param>
    /// <param name="replaceStrategy">The strategy to use when replacing a service (default is ByBoth).</param>
    /// <param name="skipIfExist">A boolean value indicating whether to skip the registration if the service already exists (default is false).</param>
    /// <param name="skipIfExistStrategy">The strategy to use when skipping a service (default is ByBoth).</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection Register(
        this IServiceCollection services,
        Type implementationType,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return Register(
            services,
            implementationType,
            implementationType,
            lifeTime,
            replaceIfExist,
            replaceStrategy,
            skipIfExist: skipIfExist,
            skipIfExistStrategy: skipIfExistStrategy);
    }

    /// <summary>
    /// Registers a service of type TService with the specified lifetime in the provided service collection.
    /// </summary>
    /// <typeparam name="TService">The type of the service to register.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="lifeTime">The lifetime of the service. Default is Transient.</param>
    /// <param name="replaceIfExist">A flag indicating whether to replace the service if it already exists. Default is true.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service is already registered. Default is ByBoth.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection Register<TService>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return Register(
            services,
            typeof(TService),
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Registers a service of type TService with a specified implementation function, lifetime, and replacement strategy.
    /// </summary>
    /// <typeparam name="TService">The type of the service to register.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="implementationFunc">A function that creates the instance of the service.</param>
    /// <param name="lifeTime">The lifetime of the service (Transient, Scoped, Singleton).</param>
    /// <param name="replaceIfExist">A flag indicating whether to replace the service if it already exists in the IServiceCollection.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service is already registered (ByService, ByImplementation, ByBoth).</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection Register<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFunc,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return Register(
            services,
            typeof(TService),
            implementationFunc,
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Registers a specific instance of type TService in the IServiceCollection.
    /// </summary>
    /// <typeparam name="TService">The type of the service to register.</typeparam>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="instance">The specific instance to register.</param>
    /// <param name="replaceIfExist">Optional parameter. If set to true, the existing registration for the service will be replaced if it exists. Default is true.</param>
    /// <param name="replaceStrategy">Optional parameter. Determines the strategy to use when replacing existing registrations. Default is CheckRegisteredStrategy.ByBoth.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    public static IServiceCollection RegisterInstance<TService>(
        this IServiceCollection services,
        TService instance,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return Register(
            services,
            typeof(TService),
            _ => instance,
            ServiceLifeTime.Singleton,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Registers a service of the specified type in the <see cref="IServiceCollection" />.
    /// </summary>
    /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationFactory">A factory that creates instances of the service type.</param>
    /// <param name="lifeTime">The <see cref="ServiceLifeTime" /> of the service (Transient, Scoped, or Singleton).</param>
    /// <param name="replaceIfExist">A boolean value indicating whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing the service (<see cref="CheckRegisteredStrategy" />).</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection Register<TImplementation>(
        this IServiceCollection services,
        Type serviceType,
        Func<IServiceProvider, TImplementation> implementationFactory,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        switch (lifeTime)
        {
            case ServiceLifeTime.Scoped:
                if (replaceIfExist)
                    services.ReplaceScoped(serviceType, implementationFactory, replaceStrategy);
                else
                    services.AddScoped(serviceType, p => implementationFactory(p));
                break;
            case ServiceLifeTime.Singleton:
                if (replaceIfExist)
                    services.ReplaceSingleton(serviceType, implementationFactory, replaceStrategy);
                else
                    services.AddSingleton(serviceType, p => implementationFactory(p));
                break;
            default:
                if (replaceIfExist)
                    services.ReplaceTransient(serviceType, implementationFactory, replaceStrategy);
                else
                    services.AddTransient(serviceType, p => implementationFactory(p));
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers a hosted service in the provided service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="hostedServiceType">The type of the hosted service to register.</param>
    /// <param name="replaceForHostedServiceType">Optional. If provided, the existing registration of this type will be replaced.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterHostedService(
        this IServiceCollection services,
        Type hostedServiceType,
        Type replaceForHostedServiceType = null)
    {
        services.Register(hostedServiceType, ServiceLifeTime.Singleton, replaceIfExist: true, replaceStrategy: CheckRegisteredStrategy.ByBoth);

        RegisterForIHostedService(services, hostedServiceType, replaceForHostedServiceType);

        return services;
    }

    /// <summary>
    /// Registers a hosted service in the provided service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="hostedServiceFactory">The hostedServiceFactory of the hosted service to register.</param>
    /// <param name="replaceForHostedServiceType">Optional. If provided, the existing registration of this type will be replaced.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterHostedService<THostedService>(
        this IServiceCollection services,
        Func<IServiceProvider, THostedService> hostedServiceFactory,
        Type replaceForHostedServiceType = null)
    {
        services.Register(hostedServiceFactory, ServiceLifeTime.Singleton, replaceIfExist: true, replaceStrategy: CheckRegisteredStrategy.ByBoth);

        RegisterForIHostedService(services, typeof(THostedService), replaceForHostedServiceType);

        return services;
    }

    private static void RegisterForIHostedService(IServiceCollection services, Type hostedServiceType, Type replaceForHostedServiceType)
    {
        RegisterHostedServiceLockDict.TryAdd(hostedServiceType.FullName!, new object());

        lock (RegisterHostedServiceLockDict[hostedServiceType.FullName!])
        {
            if (!RegisteredHostedServiceImplementTypeToImplementFactoryDict.ContainsKey(hostedServiceType.FullName!))
            {
                RegisteredHostedServiceImplementTypeToImplementFactoryDict.Add(hostedServiceType.FullName!, sp => sp.GetRequiredService(hostedServiceType));

                services
                    .Register(
                        typeof(IHostedService),
                        RegisteredHostedServiceImplementTypeToImplementFactoryDict[hostedServiceType.FullName!],
                        ServiceLifeTime.Singleton,
                        replaceIfExist: true,
                        replaceStrategy: CheckRegisteredStrategy.ByBoth);
            }
        }

        if (replaceForHostedServiceType != null)
        {
            services.RemoveWhere(
                p => p.ImplementationType == replaceForHostedServiceType ||
                     p.ImplementationInstance?.GetType() == replaceForHostedServiceType ||
                     p.ImplementationFactory == RegisteredHostedServiceImplementTypeToImplementFactoryDict[hostedServiceType.FullName!]);

            services.Register(
                replaceForHostedServiceType,
                RegisteredHostedServiceImplementTypeToImplementFactoryDict[hostedServiceType.FullName!],
                ServiceLifeTime.Singleton,
                replaceIfExist: true,
                replaceStrategy: CheckRegisteredStrategy.ByBoth);
        }
    }

    /// <summary>
    /// Registers a hosted service of the specified type in the service collection.
    /// </summary>
    /// <typeparam name="THostedService">The type of the hosted service to register. This type must implement <see cref="IHostedService" />.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="replaceForHostedServiceType">Optional. The type of the existing hosted service to replace. If this parameter is null, a new service of type <typeparamref name="THostedService" /> is added to the service collection.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterHostedService<THostedService>(
        this IServiceCollection services,
        Type replaceForHostedServiceType = null) where THostedService : class, IHostedService
    {
        return RegisterHostedService(services, typeof(THostedService), replaceForHostedServiceType);
    }

    /// <summary>
    /// Registers all types in the specified assembly that implement the IHostedService interface and match the convention of the specified type.
    /// </summary>
    /// <typeparam name="TConventionHostedService">The type that provides the convention to match. This type should implement IHostedService.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly to scan for types that match the convention.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterHostedServicesFromType<TConventionHostedService>(
        this IServiceCollection services,
        Assembly assembly) where TConventionHostedService : class, IHostedService
    {
        return RegisterHostedServicesFromType(services, assembly, typeof(TConventionHostedService));
    }

    /// <summary>
    /// Registers all types in the specified assembly that implement the IHostedService interface and match the convention of the specified type.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly to scan for types that match the convention.</param>
    /// <param name="conventionalType">The type that provides the convention to match. This type should implement IHostedService.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RegisterHostedServicesFromType(
        this IServiceCollection services,
        Assembly assembly,
        Type conventionalType)
    {
        assembly.GetTypes()
            .Where(
                implementationType => implementationType.IsClass &&
                                      !implementationType.IsAbstract &&
                                      !implementationType.IsGenericType &&
                                      implementationType.IsAssignableTo(typeof(IHostedService)) &&
                                      implementationType.IsAssignableTo(conventionalType))
            .ForEach(
                implementHostedServiceType =>
                {
                    RegisterHostedService(services, implementHostedServiceType);
                });

        return services;
    }

    /// <summary>
    /// Replaces a transient service in the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to replace the service in.</param>
    /// <param name="serviceType">The type of the service to replace.</param>
    /// <param name="implementationType">The type of the implementation to use instead.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if the service is already registered.</param>
    /// <returns>The IServiceCollection for chaining.</returns>
    public static IServiceCollection ReplaceTransient(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RemoveIfExist(
            services,
            serviceType,
            implementationType,
            replaceStrategy);

        return services.AddTransient(serviceType, implementationType);
    }

    /// <summary>
    /// Replaces the scoped service in the <see cref="IServiceCollection" /> with the specified service type and implementation type.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="serviceType">The type of the service to replace.</param>
    /// <param name="implementationType">The type of the implementation to use.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if a service is registered.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection ReplaceScoped(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RemoveIfExist(
            services,
            serviceType,
            implementationType,
            replaceStrategy);

        return services.AddScoped(serviceType, implementationType);
    }

    /// <summary>
    /// Replaces the singleton service of the specified type in the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="serviceType">The type of the service to replace.</param>
    /// <param name="implementationType">The type of the implementation to use.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if the service is already registered.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection ReplaceSingleton(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RemoveIfExist(
            services,
            serviceType,
            implementationType,
            replaceStrategy);

        return services.AddSingleton(serviceType, implementationType);
    }

    /// <inheritdoc cref="ReplaceTransient" />
    public static IServiceCollection ReplaceTransient<TImplementation>(
        this IServiceCollection services,
        Type serviceType,
        Func<IServiceProvider, TImplementation> implementationFactory,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RemoveIfExist(
            services,
            serviceType,
            typeof(TImplementation),
            replaceStrategy);

        return services.AddTransient(serviceType, p => implementationFactory(p));
    }

    /// <inheritdoc cref="ReplaceScoped" />
    public static IServiceCollection ReplaceScoped<TImplementation>(
        this IServiceCollection services,
        Type serviceType,
        Func<IServiceProvider, TImplementation> implementationFactory,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RemoveIfExist(
            services,
            serviceType,
            typeof(TImplementation),
            replaceStrategy);

        return services.AddScoped(serviceType, p => implementationFactory(p));
    }

    /// <inheritdoc cref="ReplaceSingleton" />
    public static IServiceCollection ReplaceSingleton<TImplementation>(
        this IServiceCollection services,
        Type serviceType,
        Func<IServiceProvider, TImplementation> implementationFactory,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RemoveIfExist(
            services,
            serviceType,
            typeof(TImplementation),
            replaceStrategy);

        return services.AddSingleton(serviceType, p => implementationFactory(p));
    }

    /// <inheritdoc cref="ReplaceTransient" />
    public static IServiceCollection ReplaceTransient<TService, TImplementation>(
        this IServiceCollection services,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceTransient(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    /// <inheritdoc cref="ReplaceScoped" />
    public static IServiceCollection ReplaceScoped<TService, TImplementation>(
        this IServiceCollection services,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceScoped(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    /// <inheritdoc cref="ReplaceSingleton" />
    public static IServiceCollection ReplaceSingleton<TService, TImplementation>(
        this IServiceCollection services,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceSingleton(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    /// <summary>
    /// Removes the service from the <see cref="IServiceCollection" /> if it exists and matches the provided predicate.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to remove the service from.</param>
    /// <param name="predicate">A function to test each service for a condition.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection RemoveIfExist(
        this IServiceCollection services,
        Func<ServiceDescriptor, bool> predicate)
    {
        var existedServiceRegister = services.FirstOrDefault(predicate);

        if (existedServiceRegister != null) services.Remove(existedServiceRegister);

        return services;
    }

    /// <summary>
    /// Removes the specified service from the <see cref="IServiceCollection" /> if it exists.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to remove the service from.</param>
    /// <param name="serviceType">The type of the service to remove.</param>
    /// <param name="implementationType">The type of the implementation to remove.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if the service exists. Default is <see cref="CheckRegisteredStrategy.ByBoth" />.</param>
    /// <returns>The <see cref="IServiceCollection" /> after the service has been removed if it existed.</returns>
    public static IServiceCollection RemoveIfExist(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        return replaceStrategy switch
        {
            CheckRegisteredStrategy.ByService => RemoveIfExist(services, p => p.ServiceType == serviceType),
            CheckRegisteredStrategy.ByImplementation => RemoveIfExist(
                services,
                p => p.ImplementationType == implementationType),
            CheckRegisteredStrategy.ByBoth => RemoveIfExist(
                services,
                p => p.ServiceType == serviceType && p.ImplementationType == implementationType),
            _ => throw new ArgumentOutOfRangeException(nameof(replaceStrategy), replaceStrategy, null)
        };
    }

    // Repeat the same pattern for the other RemoveIfExist method
    /// <summary>
    /// Registers all interfaces for the specified implementation type in the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="implementationType">The type of the implementation to register interfaces for.</param>
    /// <param name="lifeTime">The lifetime of the services.</param>
    /// <param name="replaceIfExist">Whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when checking if the service is already registered.</param>
    /// <param name="skipIfExist">Whether to skip the service if it already exists.</param>
    /// <param name="skipIfExistStrategy">The strategy to use when checking if the service should be skipped.</param>
    public static void RegisterInterfacesForImplementation(
        this IServiceCollection services,
        Type implementationType,
        ServiceLifeTime lifeTime,
        bool replaceIfExist,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth,
        bool skipIfExist = false,
        CheckRegisteredStrategy skipIfExistStrategy = CheckRegisteredStrategy.ByBoth)
    {
        if (!implementationType.IsGenericType)
            implementationType
                .GetInterfaces()
                .Where(implementationTypeInterface => !implementationTypeInterface.IsGenericType)
                .Where(DefaultIgnoreRegisterLibraryInterfacesForImplementationExpr())
                .ForEach(
                    implementationTypeInterface => services.Register(
                        implementationTypeInterface.FixMissingFullNameGenericType(),
                        implementationType,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy,
                        skipIfExist: skipIfExist,
                        skipIfExistStrategy: skipIfExistStrategy));

        else
            implementationType
                .GetInterfaces()
                .Where(implementationType.MatchGenericArguments)
                .Where(DefaultIgnoreRegisterLibraryInterfacesForImplementationExpr())
                .ForEach(
                    implementationTypeInterface => services.Register(
                        implementationTypeInterface.FixMissingFullNameGenericType(),
                        implementationType,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy,
                        skipIfExist: skipIfExist,
                        skipIfExistStrategy: skipIfExistStrategy));
    }

    public static Func<Type, bool> DefaultIgnoreRegisterLibraryInterfacesForImplementationExpr()
    {
        return implementationTypeInterface =>
            DefaultIgnoreRegisterLibraryInterfacesNameSpacePrefixes.NotExist(prefix => implementationTypeInterface.FullName?.StartsWith(prefix) == true);
    }

    /// <summary>
    /// Registers the interfaces for the specified implementation in the provided services collection.
    /// </summary>
    /// <typeparam name="TImplementation">The type of the implementation to register.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the service to.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <param name="lifeTime">The <see cref="ServiceLifeTime" /> of the service.</param>
    /// <param name="replaceIfExist">A boolean value indicating whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing the service.</param>
    public static void RegisterInterfacesForImplementation<TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> implementationFactory,
        ServiceLifeTime lifeTime,
        bool replaceIfExist,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        RegisterInterfacesForImplementation(
            services,
            typeof(TImplementation),
            provider => implementationFactory(provider),
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Registers the interfaces for the specified implementation type.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the service to.</param>
    /// <param name="implementationType">The type of the implementation to register.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <param name="lifeTime">The lifetime of the service.</param>
    /// <param name="replaceIfExist">A flag indicating whether to replace the service if it already exists.</param>
    /// <param name="replaceStrategy">The strategy to use when replacing the service.</param>
    public static void RegisterInterfacesForImplementation(
        this IServiceCollection services,
        Type implementationType,
        Func<IServiceProvider, object> implementationFactory,
        ServiceLifeTime lifeTime,
        bool replaceIfExist,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
    {
        if (implementationType.IsGenericType)
            implementationType
                .GetInterfaces()
                .Where(implementationType.MatchGenericArguments)
                .Where(DefaultIgnoreRegisterLibraryInterfacesForImplementationExpr())
                .ForEach(
                    implementationTypeInterface => services.Register(
                        implementationTypeInterface.FixMissingFullNameGenericType(),
                        implementationFactory,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy));
        else
            implementationType
                .GetInterfaces()
                .Where(p => !p.IsGenericType)
                .Where(DefaultIgnoreRegisterLibraryInterfacesForImplementationExpr())
                .ForEach(
                    implementationTypeInterface => services.Register(
                        implementationTypeInterface.FixMissingFullNameGenericType(),
                        implementationFactory,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy));
    }

    /// <summary>
    /// manuallyParams to override using based on param index position. <br />
    /// Example: method = (T1 param1, T2 param2); serviceProvider.ResolveMethodParameters(method, null, customParam2Value) equal to method(serviceProvider.GetService[T1](),customParam2Value)
    /// </summary>
    public static object[] ResolveMethodParameters(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = method.Method.GetParameters()
            .Select(
                (parameterInfo, index) =>
                {
                    // If params at the current index is given and not null/default value, use the manually given param
                    if (manuallyParams.Any() &&
                        manuallyParams.Length > index &&
                        manuallyParams[index] != null &&
                        manuallyParams[index] != parameterInfo.ParameterType.GetDefaultValue())
                        return manuallyParams[index];

                    return parameterInfo.ParameterType.IsClass || parameterInfo.ParameterType.IsInterface
                        ? serviceProvider.GetService(parameterInfo.ParameterType)
                        : parameterInfo.ParameterType.GetDefaultValue();
                })
            .ToArray();
        return parameters;
    }

    public static object[] ResolveMethodParameters(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        return scope.ServiceProvider.ResolveMethodParameters(method, manuallyParams);
    }

    /// <summary>
    /// Execute method with params injection. <br />
    /// If method = (T1 param1, T2 param2) then it equivalent to method(serviceProvider.GetService[T1](),serviceProvider.GetService[T2]>()) <br />
    /// manuallyParams to override using based on param index and it's not null. <br />
    /// Example: serviceProvider.ExecuteInject(method, null, customParam2Value) equal to method(serviceProvider.GetService[T1](),customParam2Value)
    /// </summary>
    public static void ExecuteInject(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        method.DynamicInvoke(parameters);
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static TResult ExecuteInject<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters).Cast<TResult>();

        return result;
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static async Task ExecuteInjectAsync(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task>() != null) await result.As<Task>();
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static async Task<TResult> ExecuteInjectAsync<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task<TResult>>() != null) return await result.As<Task<TResult>>();

        return result.Cast<TResult>();
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static void ExecuteInject(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        method.DynamicInvoke(parameters);
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static TResult ExecuteInject<TResult>(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters).Cast<TResult>();

        return result;
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static async Task ExecuteInjectAsync(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task>() != null) await result.As<Task>();
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate,object[])" />
    public static async Task<TResult> ExecuteInjectAsync<TResult>(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        method.Method
            .Validate(
                must: methodInfo => methodInfo.GetParameters().Length >= manuallyParams.Length &&
                                    manuallyParams.All(
                                        (manuallyParam, index) =>
                                            manuallyParam?.GetType() == null || manuallyParam.GetType() == methodInfo.GetParameters()[index].ParameterType),
                errorMsg: "Delegate method parameters signature must start with all parameters correspond to manuallyParams")
            .EnsureValid();

        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task<TResult>>() != null) return await result.As<Task<TResult>>();

        return result.Cast<TResult>();
    }

    /// <summary>
    /// Run method in new scope. Equivalent to: using(var scope = serviceProvider.CreateScope()) { method(scope); }
    /// </summary>
    public static void ExecuteScoped(this IServiceProvider serviceProvider, Action<IServiceScope> method)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            method(scope);
        }
    }

    /// <inheritdoc cref="ExecuteScoped" />
    public static TResult ExecuteScoped<TResult>(this IServiceProvider serviceProvider, Func<IServiceScope, TResult> method)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var result = method(scope);

            result.As<Task>()?.WaitResult();

            return result;
        }
    }

    /// <inheritdoc cref="ExecuteScoped" />
    public static async Task ExecuteScopedAsync(this IServiceProvider serviceProvider, Func<IServiceScope, Task> method)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            await method(scope);
        }
    }

    /// <inheritdoc cref="ExecuteScoped" />
    public static async Task<TResult> ExecuteScopedAsync<TResult>(this IServiceProvider serviceProvider, Func<IServiceScope, Task<TResult>> method)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            return await method(scope);
        }
    }

    /// <summary>
    /// Execute method with params injection in a new scope. <br />
    /// If method = (T1 param1, T2 param2) then it equivalent to using(var scope = serviceProvider.CreateScope()) => method(scope.ServiceProvider.GetService[T1](), scope.ServiceProvider.GetService[T2]>())
    /// manuallyParams to override using based on param index and it's not null. <br />
    /// Example: serviceProvider.ExecuteInject(method, null, customParam2Value) equal to method(serviceProvider.GetService[T1](),customParam2Value)
    /// </summary>
    public static void ExecuteInjectScoped(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        serviceProvider.ExecuteScoped(scope => scope.ExecuteInject(method, manuallyParams));
    }

    /// <inheritdoc cref="ExecuteInjectScoped" />
    public static TResult ExecuteInjectScoped<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        return serviceProvider.ExecuteScoped(scope => scope.ExecuteInject<TResult>(method, manuallyParams));
    }

    /// <inheritdoc cref="ExecuteInjectScoped" />
    public static Task ExecuteInjectScopedAsync(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        return serviceProvider.ExecuteScopedAsync(scope => scope.ExecuteInjectAsync(method, manuallyParams));
    }

    /// <inheritdoc cref="ExecuteInjectScoped" />
    public static Task<TResult> ExecuteInjectScopedAsync<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        return serviceProvider.ExecuteScopedAsync(scope => scope.ExecuteInjectAsync<TResult>(method, manuallyParams));
    }

    public static bool CheckHasRegisteredScopedService<TService>(this IServiceProvider serviceProvider)
    {
        return CheckHasRegisteredScopedService(serviceProvider, typeof(TService));
    }

    public static bool CheckHasRegisteredScopedService(this IServiceProvider serviceProvider, Type serviceType)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            return scope.ServiceProvider.GetService(serviceType) != null;
        }
    }

    /// <inheritdoc cref="ExecuteInjectScopedPagingAsync(IServiceProvider,long,int,TimeSpan?,Delegate,object[])" />
    public static async Task ExecuteInjectScopedPagingAsync(
        this IServiceProvider serviceProvider,
        long maxItemCount,
        int pageSize,
        Delegate method,
        params object[] manuallyParams)
    {
        await ExecuteInjectScopedPagingAsync(serviceProvider, maxItemCount, pageSize, null, method, manuallyParams);
    }

    /// <summary>
    /// Support ExecuteInjectScopedAsync paged. <br />
    /// Method to be executed, the First two parameter MUST BE (int skipCount, int pageSize). <br />
    /// Then the "manuallyParams" for the method. And the last will be the object you want to be dependency injected
    /// </summary>
    /// <param name="maxItemCount">Max items count</param>
    /// <param name="serviceProvider">serviceProvider</param>
    /// <param name="pageSize">Page size to execute.</param>
    /// <param name="pageDelayTime">Delay time between pages</param>
    /// <param name="method">method to be executed. First two parameter MUST BE (int skipCount, int pageSize)</param>
    /// <param name="manuallyParams"></param>
    /// <returns>Task.</returns>
    public static async Task ExecuteInjectScopedPagingAsync(
        this IServiceProvider serviceProvider,
        long maxItemCount,
        int pageSize,
        TimeSpan? pageDelayTime,
        Delegate method,
        params object[] manuallyParams)
    {
        method.Method
            .Validate(
                must: p => p.GetParameters().Length >= 2 && p.GetParameters().Take(2).All(_ => _.ParameterType == typeof(int)),
                "Method parameters must start with (int skipCount, int pageSize)")
            .EnsureValid();

        await Util.Pager.ExecutePagingAsync(
            async (skipCount, pageSize) =>
            {
                try
                {
                    await serviceProvider.ExecuteInjectScopedAsync(
                        method,
                        manuallyParams: Util.ListBuilder.NewArray<object>(skipCount, pageSize).Concat(manuallyParams).ToArray());
                }
                finally
                {
                    _ = Task.Run(
                        () => Util.GarbageCollector.Collect(),
                        CancellationToken.None);
                }
            },
            maxItemCount: maxItemCount,
            pageSize: pageSize,
            pageDelayTime: pageDelayTime);
    }

    /// <summary>
    /// Support ExecuteInjectScopedAsync scrolling paging. <br />
    /// Then the "manuallyParams" for the method. And the last will be the object you want to be dependency injected
    /// </summary>
    public static Task ExecuteInjectScopedScrollingPagingAsync<TItem>(
        this IServiceProvider serviceProvider,
        int maxExecutionCount,
        Delegate method,
        params object[] manuallyParams)
    {
        return Util.Pager.ExecuteScrollingPagingAsync(
            async () =>
            {
                try
                {
                    return await serviceProvider.ExecuteInjectScopedAsync<List<TItem>>(method, manuallyParams);
                }
                finally
                {
                    _ = Task.Run(
                        () => Util.GarbageCollector.Collect(),
                        CancellationToken.None);
                }
            },
            maxExecutionCount);
    }

    /// <summary>
    /// Support ExecuteInjectScopedAsync scrolling paging. <br />
    /// Then the "manuallyParams" for the method. And the last will be the object you want to be dependency injected
    /// </summary>
    public static Task ExecuteInjectScopedScrollingPagingAsync<TItem>(
        this IServiceProvider serviceProvider,
        int maxExecutionCount,
        TimeSpan? pageDelayTime,
        Delegate method,
        params object[] manuallyParams)
    {
        return Util.Pager.ExecuteScrollingPagingAsync(
            async () =>
            {
                try
                {
                    return await serviceProvider.ExecuteInjectScopedAsync<List<TItem>>(method, manuallyParams);
                }
                finally
                {
                    _ = Task.Run(
                        () => Util.GarbageCollector.Collect(),
                        CancellationToken.None);
                }
            },
            maxExecutionCount,
            pageDelayTime);
    }

    public enum CheckRegisteredStrategy
    {
        ByService,
        ByImplementation,
        ByBoth
    }
}
