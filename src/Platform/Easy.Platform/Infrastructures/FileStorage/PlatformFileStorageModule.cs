using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.FileStorage;

public abstract class PlatformFileStorageModule : PlatformInfrastructureModule
{
    public PlatformFileStorageModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.Register(FileStorageOptionsProvider, ServiceLifeTime.Singleton);
    }

    protected abstract PlatformFileStorageOptions FileStorageOptionsProvider(IServiceProvider serviceProvider);
}
