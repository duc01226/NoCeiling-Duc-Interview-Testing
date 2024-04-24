using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Common;

/// <summary>
/// The service provider scope is Singleton, which is global and not scoped, never be disposed unless the application is stopped
/// </summary>
public interface IPlatformRootServiceProvider : IServiceProvider
{
    public bool CheckServiceRegistered(Type serviceType);

    public bool CheckAssignableToServiceRegistered(Type assignableToServiceType);
}

public class PlatformRootServiceProvider : IPlatformRootServiceProvider
{
    private readonly ConcurrentDictionary<Type, bool> assignableToServiceTypeRegisteredDict = new();
    private readonly Lazy<HashSet<Type>> registeredServiceTypesLazy;
    private readonly IServiceProvider serviceProvider;
    private readonly IServiceCollection services;

    public PlatformRootServiceProvider(IServiceProvider serviceProvider, IServiceCollection services)
    {
        this.serviceProvider = serviceProvider;
        this.services = services;
        registeredServiceTypesLazy = new Lazy<HashSet<Type>>(() => services.Select(p => p.ServiceType).ToHashSet());
    }

    public object GetService(Type serviceType)
    {
        return serviceProvider?.GetService(serviceType);
    }

    public bool CheckServiceRegistered(Type serviceType)
    {
        return registeredServiceTypesLazy.Value.Contains(serviceType);
    }

    public bool CheckAssignableToServiceRegistered(Type assignableToServiceType)
    {
        if (assignableToServiceTypeRegisteredDict.ContainsKey(assignableToServiceType) == false)
            assignableToServiceTypeRegisteredDict.TryAdd(assignableToServiceType, InternalCheckAssignableToServiceRegistered(assignableToServiceType));

        return assignableToServiceTypeRegisteredDict[assignableToServiceType];
    }

    private bool InternalCheckAssignableToServiceRegistered(Type assignableToServiceType)
    {
        return services.Any(
            sd => sd.ImplementationType?.IsAssignableTo(assignableToServiceType) == true ||
                  sd.ImplementationType?.IsAssignableToGenericType(assignableToServiceType) == true);
    }
}
