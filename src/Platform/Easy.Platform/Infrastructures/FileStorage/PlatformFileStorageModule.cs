using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Easy.Platform.Infrastructures.FileStorage;

public abstract class PlatformFileStorageModule : PlatformInfrastructureModule
{
    public PlatformFileStorageModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.TryAddTransient(FileStorageOptionsProvider);
    }

    protected abstract PlatformFileStorageOptions FileStorageOptionsProvider(IServiceProvider serviceProvider);
}
