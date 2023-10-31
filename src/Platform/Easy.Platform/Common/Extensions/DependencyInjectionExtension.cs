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
    private static readonly Dictionary<string, Func<IServiceProvider, object>> RegisteredHostedServiceImplementTypeToImplementFactoryDict = new();
    private static readonly ConcurrentDictionary<string, object> RegisterHostedServiceLockDict = new();

    public static string[] DefaultIgnoreRegisterLibraryInterfacesNameSpacePrefixes { get; set; } = { "System", "Microsoft" };

    /// <summary>
    /// Register all concrete types in a module that is assignable to TConventional as itself and it's implemented
    /// interfaces
    /// </summary>
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
    /// Register all concrete types in a module that is assignable to TConventional as itself
    /// </summary>
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
    /// Register all concrete types in a module that is assignable to TConventional as itself and it's implemented
    /// interfaces
    /// </summary>
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
    /// Register all concrete types in a module that is assignable to TConventional as itself
    /// </summary>
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
    /// Register TImplementation as itself and it's implemented interfaces
    /// </summary>
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
    /// Register TImplementation as itself and it's implemented interfaces
    /// </summary>
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
    ///     <inheritdoc cref="RegisterAllForImplementation(IServiceCollection,Type,ServiceLifeTime,bool,CheckRegisteredStrategy)" />
    /// </summary>
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
    /// Register TImplementation instance from implementationFactory as itself and it's implemented interfaces
    /// </summary>
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

    public static IServiceCollection RegisterHostedService(
        this IServiceCollection services,
        Type hostedServiceType,
        Type replaceForHostedServiceType = null)
    {
        services.Register(hostedServiceType, ServiceLifeTime.Singleton, replaceIfExist: true, replaceStrategy: CheckRegisteredStrategy.ByBoth);

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

        return services;
    }

    public static IServiceCollection RegisterHostedService<THostedService>(
        this IServiceCollection services,
        Type replaceForHostedServiceType = null) where THostedService : class, IHostedService
    {
        return RegisterHostedService(services, typeof(THostedService), replaceForHostedServiceType);
    }

    public static IServiceCollection RegisterHostedServicesFromType<TConventionHostedService>(
        this IServiceCollection services,
        Assembly assembly) where TConventionHostedService : class, IHostedService
    {
        return RegisterHostedServicesFromType(services, assembly, typeof(TConventionHostedService));
    }

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

    public static IServiceCollection ReplaceTransient<TService, TImplementation>(
        this IServiceCollection services,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceTransient(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    public static IServiceCollection ReplaceScoped<TService, TImplementation>(
        this IServiceCollection services,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceScoped(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    public static IServiceCollection ReplaceSingleton<TService, TImplementation>(
        this IServiceCollection services,
        CheckRegisteredStrategy replaceStrategy = CheckRegisteredStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceSingleton(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    public static IServiceCollection RemoveIfExist(
        this IServiceCollection services,
        Func<ServiceDescriptor, bool> predicate)
    {
        var existedServiceRegister = services.FirstOrDefault(predicate);

        if (existedServiceRegister != null) services.Remove(existedServiceRegister);

        return services;
    }

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

    /// <summary>
    /// Support ExecuteInjectScopedAsync paged. <br />
    /// Method to be executed, the First two parameter MUST BE (int skipCount, int pageSize). <br />
    /// Then the "manuallyParams" for the method. And the last will be the object you want to be dependency injected
    /// </summary>
    /// <param name="maxItemCount">Max items count</param>
    /// <param name="serviceProvider">serviceProvider</param>
    /// <param name="pageSize">Page size to execute.</param>
    /// <param name="method">method to be executed. First two parameter MUST BE (int skipCount, int pageSize)</param>
    /// <param name="manuallyParams"></param>
    /// <returns>Task.</returns>
    public static async Task ExecuteInjectScopedPagingAsync(
        this IServiceProvider serviceProvider,
        long maxItemCount,
        int pageSize,
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
                await serviceProvider.ExecuteInjectScopedAsync(
                    method,
                    manuallyParams: Util.ListBuilder.NewArray<object>(skipCount, pageSize).Concat(manuallyParams).ToArray());
            },
            maxItemCount: maxItemCount,
            pageSize: pageSize);
    }

    public static async Task ExecuteInjectScopedPagingAsync(
        this IServiceProvider serviceProvider,
        long maxItemCount,
        int pageSize,
        int maxRetryCount,
        int retrySleepMilliseconds,
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
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    () => serviceProvider.ExecuteInjectScopedAsync(
                        method,
                        manuallyParams: Util.ListBuilder.NewArray<object>(skipCount, pageSize).Concat(manuallyParams).ToArray()),
                    retryCount: maxRetryCount,
                    sleepDurationProvider: _ => retrySleepMilliseconds.Milliseconds());
            },
            maxItemCount: maxItemCount,
            pageSize: pageSize);
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
            () => serviceProvider.ExecuteInjectScopedAsync<List<TItem>>(method, manuallyParams),
            maxExecutionCount);
    }

    public enum CheckRegisteredStrategy
    {
        ByService,
        ByImplementation,
        ByBoth
    }
}
