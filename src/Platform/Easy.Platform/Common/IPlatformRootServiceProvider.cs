namespace Easy.Platform.Common;

/// <summary>
/// The service provider scope is Singleton, which is global and not scoped, never be disposed unless the application is stopped
/// </summary>
public interface IPlatformRootServiceProvider : IServiceProvider
{
}

public class PlatformRootServiceProvider : IPlatformRootServiceProvider
{
    private readonly IServiceProvider serviceProvider;

    public PlatformRootServiceProvider(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public object GetService(Type serviceType)
    {
        return serviceProvider?.GetService(serviceType);
    }
}
