using System.Reflection;
using Easy.Platform.Common.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Easy.Platform.Common.Extensions;

public static class DependencyInjectionExtension
{
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
                    services.Register(implementationType, lifeTime, replaceIfExist);

                    services.RegisterInterfacesForImplementation(
                        implementationType,
                        lifeTime,
                        replaceIfExist,
                        replaceStrategy);
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        return RegisterAllFromType(
            services,
            typeof(TConventional),
            assembly,
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    /// <summary>
    /// Register TImplementation as itself and it's implemented interfaces
    /// </summary>
    public static IServiceCollection RegisterAllForImplementation(
        this IServiceCollection services,
        Type implementationType,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        services.RegisterIfNotExist(implementationType, implementationType, lifeTime);

        services.RegisterInterfacesForImplementation(
            implementationType,
            lifeTime,
            replaceIfExist,
            replaceStrategy);

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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        services.RegisterIfNotExist(implementationType, implementationType, lifeTime);

        services.RegisterInterfacesForImplementation(
            implementationType,
            implementationFactory,
            lifeTime,
            replaceIfExist,
            replaceStrategy);

        return services;
    }

    /// <summary>
    ///     <inheritdoc cref="RegisterAllForImplementation(IServiceCollection,Type,ServiceLifeTime,bool,ReplaceServiceStrategy)" />
    /// </summary>
    public static IServiceCollection RegisterAllForImplementation<TImplementation>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ServiceLifeTime lifeTime)
    {
        return RegisterIfNotExist(
            services,
            typeof(TService),
            typeof(TImplementation),
            lifeTime);
    }

    public static IServiceCollection RegisterIfNotExist(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifeTime lifeTime)
    {
        if (services.Any(p => p.ServiceType == serviceType && p.ImplementationType == implementationType)) return services;

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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        return Register(
            services,
            implementationType,
            implementationType,
            lifeTime,
            replaceIfExist,
            replaceStrategy);
    }

    public static IServiceCollection Register<TService>(
        this IServiceCollection services,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        Func<IServiceProvider, TImplementation> implementationFunc,
        ServiceLifeTime lifeTime = ServiceLifeTime.Transient,
        bool replaceIfExist = true,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        switch (lifeTime)
        {
            case ServiceLifeTime.Scoped:
                if (replaceIfExist)
                    services.ReplaceScoped(serviceType, implementationFunc, replaceStrategy);
                else
                    services.AddScoped(serviceType, p => implementationFunc(p));
                break;
            case ServiceLifeTime.Singleton:
                if (replaceIfExist)
                    services.ReplaceSingleton(serviceType, implementationFunc, replaceStrategy);
                else
                    services.AddSingleton(serviceType, p => implementationFunc(p));
                break;
            default:
                if (replaceIfExist)
                    services.ReplaceTransient(serviceType, implementationFunc, replaceStrategy);
                else
                    services.AddTransient(serviceType, p => implementationFunc(p));
                break;
        }

        return services;
    }

    public static IServiceCollection RegisterHostedService(
        this IServiceCollection services,
        Type hostedServiceType,
        bool replaceIfExist = true,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        return services.Register(
            typeof(IHostedService),
            hostedServiceType,
            ServiceLifeTime.Singleton,
            replaceIfExist,
            replaceStrategy);
    }

    public static IServiceCollection RegisterHostedService<THostedService>(
        this IServiceCollection services,
        bool replaceIfExist = true,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth) where THostedService : class, IHostedService
    {
        return services.RegisterHostedService(
            typeof(THostedService),
            replaceIfExist,
            replaceStrategy);
    }

    public static IServiceCollection ReplaceTransient(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceTransient(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    public static IServiceCollection ReplaceScoped<TService, TImplementation>(
        this IServiceCollection services,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceScoped(typeof(TService), typeof(TImplementation), replaceStrategy);
    }

    public static IServiceCollection ReplaceSingleton<TService, TImplementation>(
        this IServiceCollection services,
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        return replaceStrategy switch
        {
            ReplaceServiceStrategy.ByService => RemoveIfExist(services, p => p.ServiceType == serviceType),
            ReplaceServiceStrategy.ByImplementation => RemoveIfExist(
                services,
                p => p.ImplementationType == implementationType),
            ReplaceServiceStrategy.ByBoth => RemoveIfExist(
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
    {
        if (implementationType.IsGenericType)
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
                        replaceStrategy));
        else
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
                        replaceStrategy));
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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
        ReplaceServiceStrategy replaceStrategy = ReplaceServiceStrategy.ByBoth)
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

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static TResult ExecuteInject<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters).Cast<TResult>();

        return result;
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static async Task ExecuteInjectAsync(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task>() != null) await result.As<Task>();
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static async Task<TResult> ExecuteInjectAsync<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        var parameters = serviceProvider.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task<TResult>>() != null) return await result.As<Task<TResult>>();

        return result.Cast<TResult>();
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static void ExecuteInject(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        method.DynamicInvoke(parameters);
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static TResult ExecuteInject<TResult>(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters).Cast<TResult>();

        return result;
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static async Task ExecuteInjectAsync(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
        var parameters = scope.ResolveMethodParameters(method, manuallyParams);

        var result = method.DynamicInvoke(parameters);

        if (result.As<Task>() != null) await result.As<Task>();
    }

    /// <inheritdoc cref="ExecuteInject(IServiceProvider,Delegate)" />
    public static async Task<TResult> ExecuteInjectAsync<TResult>(this IServiceScope scope, Delegate method, params object[] manuallyParams)
    {
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
            return method(scope);
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
    public static async Task ExecuteInjectScopedAsync(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        await serviceProvider.ExecuteScopedAsync(scope => scope.ExecuteInjectAsync(method, manuallyParams));
    }

    /// <inheritdoc cref="ExecuteInjectScoped" />
    public static async Task<TResult> ExecuteInjectScopedAsync<TResult>(this IServiceProvider serviceProvider, Delegate method, params object[] manuallyParams)
    {
        return await serviceProvider.ExecuteScopedAsync(scope => scope.ExecuteInjectAsync<TResult>(method, manuallyParams));
    }

    public enum ReplaceServiceStrategy
    {
        ByService,
        ByImplementation,
        ByBoth
    }
}
