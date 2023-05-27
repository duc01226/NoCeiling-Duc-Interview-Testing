using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.AutomationTest.Extensions.DependencyInjection;

public class ExposeServiceCollectionFactory : IServiceProviderFactory<IServiceCollection>
{
    private readonly DefaultServiceProviderFactory defaultFactory = new();

    public IServiceCollection ServiceCollection { get; private set; } = new ServiceCollection();

    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        ServiceCollection = services;
        return defaultFactory.CreateBuilder(services);
    }

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        return defaultFactory.CreateServiceProvider(containerBuilder);
    }
}
