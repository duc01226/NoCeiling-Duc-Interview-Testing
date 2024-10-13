using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Common;

/// <summary>
/// The service provider scope is Singleton, which is global and not scoped, never be disposed unless the application is stopped
/// </summary>
public interface IPlatformRootServiceProvider : IServiceProvider
{
    /// <summary>
    /// Check serviceType is registered in services collection
    /// </summary>
    public bool IsServiceTypeRegistered(Type serviceType);

    /// <summary>
    /// Check any implementation type is assignable to assignableToServiceType registered
    /// </summary>
    public bool IsAnyImplementationAssignableToServiceTypeRegistered(Type assignableToServiceType);

    /// <summary>
    /// Get type by type name in registered platform module assemblies
    /// </summary>
    public Type GetRegisteredPlatformModuleAssembliesType(string typeName);
}

public class PlatformRootServiceProvider : IPlatformRootServiceProvider
{
    private readonly ConcurrentDictionary<Type, bool> assignableToServiceTypeRegisteredDict = new();
    private readonly ConcurrentDictionary<string, Type> registeredPlatformModuleAssembliesTypeByNameCachedDict = new();
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

    public bool IsServiceTypeRegistered(Type serviceType)
    {
        return registeredServiceTypesLazy.Value.Contains(serviceType);
    }

    public bool IsAnyImplementationAssignableToServiceTypeRegistered(Type assignableToServiceType)
    {
        return assignableToServiceTypeRegisteredDict.GetOrAdd(
            assignableToServiceType,
            assignableToServiceType => InternalCheckIsAnyImplementationAssignableToServiceTypeRegistered(assignableToServiceType));
    }

    public Type GetRegisteredPlatformModuleAssembliesType(string typeName)
    {
        return registeredPlatformModuleAssembliesTypeByNameCachedDict.GetOrAdd(
            typeName,
            typeName => InternalGetRegisteredPlatformModuleAssembliesType(typeName));
    }

    private Type InternalGetRegisteredPlatformModuleAssembliesType(string typeName)
    {
        var scanAssemblies = serviceProvider.GetServices<PlatformModule>()
            .SelectMany(p => p.GetServicesRegisterScanAssemblies())
            .ConcatSingle(typeof(PlatformModule).Assembly)
            .ToList();

        var scannedResultType = scanAssemblies
            .Select(p => p.GetType(typeName))
            .FirstOrDefault(p => p != null)
            .Pipe(scannedResultType => scannedResultType ?? Type.GetType(typeName, throwOnError: false));
        return scannedResultType;
    }

    private bool InternalCheckIsAnyImplementationAssignableToServiceTypeRegistered(Type assignableToServiceType)
    {
        return services.Any(
            sd =>
            {
                var typeToCheck = sd.ImplementationType ?? sd.ImplementationFactory?.Method.ReturnType;

                return typeToCheck?.IsClass == true &&
                       (typeToCheck?.IsAssignableTo(assignableToServiceType) == true ||
                        typeToCheck?.IsAssignableToGenericType(assignableToServiceType) == true);
            });
    }
}
