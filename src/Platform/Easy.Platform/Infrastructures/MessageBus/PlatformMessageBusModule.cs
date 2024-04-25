using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.MessageBus;

public abstract class PlatformMessageBusModule : PlatformInfrastructureModule
{
    protected PlatformMessageBusModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllSelfImplementationFromType<IPlatformMessageBusProducer>(Assembly);
        serviceCollection.RegisterAllSelfImplementationFromType<IPlatformMessageBusConsumer>(Assembly);
        serviceCollection.RegisterAllFromType<IPlatformSelfRoutingKeyBusMessage>(Assembly);
        serviceCollection.RegisterIfServiceNotExist<IPlatformMessageBusScanner, PlatformMessageBusScanner>(ServiceLifeTime.Singleton);
        serviceCollection.Register(typeof(PlatformMessageBusConfig), MessageBusConfigFactory, ServiceLifeTime.Singleton);
    }

    protected virtual PlatformMessageBusConfig MessageBusConfigFactory(IServiceProvider sp)
    {
        return new PlatformMessageBusConfig();
    }
}
