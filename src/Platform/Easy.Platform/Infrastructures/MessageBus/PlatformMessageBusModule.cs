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

    // Init MessageBus before any other infrastructure module but still after the next level priority module (Persistence Module)
    public override int ExecuteInitPriority => DefaultExecuteInitPriority + ExecuteInitPriorityNextLevelDistance - 1;

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformMessageBusProducer>(Assembly);
        serviceCollection.RegisterAllFromType<IPlatformMessageBusConsumer>(Assembly);
        serviceCollection.RegisterAllFromType<IPlatformSelfRoutingKeyBusMessage>(Assembly);
        serviceCollection.RegisterIfServiceNotExist<IPlatformMessageBusScanner, PlatformMessageBusScanner>(ServiceLifeTime.Singleton);
    }
}
