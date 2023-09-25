using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// All process main started from PlatformRabbitMqHostedService. Please look at it for more information.
/// Send message via PlatformRabbitMqMessageBusProducer
/// </summary>
public abstract class PlatformRabbitMqMessageBusModule : PlatformMessageBusModule
{
    protected PlatformRabbitMqMessageBusModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public override string[] TracingSources()
    {
        return Util.ListBuilder.NewArray(
            IPlatformMessageBusProducer.ActivitySource.Name,
            PlatformRabbitMqProcessInitializerService.ActivitySource.Name);
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        // PlatformRabbitMqChannelPool hold rabbitmq connection which should be singleton
        serviceCollection.Register<PlatformProducerRabbitMqChannelPool>(ServiceLifeTime.Singleton);
        serviceCollection.Register<PlatformConsumerRabbitMqChannelPool>(ServiceLifeTime.Singleton);
        serviceCollection.Register<PlatformRabbitMqChannelPoolPolicy>();

        serviceCollection.Register<IPlatformRabbitMqExchangeProvider, PlatformRabbitMqExchangeProvider>();
        serviceCollection.Register(RabbitMqOptionsFactory);
        serviceCollection.Register<IPlatformMessageBusProducer, PlatformRabbitMqMessageBusProducer>();
        serviceCollection.Register<PlatformRabbitMqProcessInitializerService>(ServiceLifeTime.Singleton);
        serviceCollection.RegisterHostedService<PlatformRabbitMqStartProcessHostedService>();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        Util.TaskRunner.QueueActionInBackground(
            () => ServiceProvider.GetRequiredService<PlatformRabbitMqProcessInitializerService>().StartProcess(CancellationToken.None),
            () => Logger);
    }

    protected abstract PlatformRabbitMqOptions RabbitMqOptionsFactory(IServiceProvider serviceProvider);
}
