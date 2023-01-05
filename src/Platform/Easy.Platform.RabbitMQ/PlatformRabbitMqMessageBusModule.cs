using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Infrastructures.MessageBus;
using Easy.Platform.RabbitMQ.Inbox;
using Easy.Platform.RabbitMQ.Outbox;
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
            PlatformRabbitMqMessageBusProducer.ActivitySource.Name,
            PlatformRabbitMqProcessInitializerService.ActivitySource.Name);
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        // PlatformRabbitMqChannelPool and PlatformRabbitMqChannelPoolPolicy hold rabbitmq connection which should be singleton
        serviceCollection.Register<PlatformRabbitMqChannelPoolPolicy>(ServiceLifeTime.Singleton);
        serviceCollection.Register<PlatformRabbitMqChannelPool>(ServiceLifeTime.Singleton);

        serviceCollection.Register<IPlatformRabbitMqExchangeProvider, PlatformRabbitMqExchangeProvider>();
        serviceCollection.Register(RabbitMqOptionsFactory);
        serviceCollection.Register<IPlatformMessageBusProducer, PlatformRabbitMqMessageBusProducer>();
        serviceCollection.Register<PlatformRabbitMqProcessInitializerService>(ServiceLifeTime.Singleton);
        serviceCollection.RegisterHostedService<PlatformRabbitMqStartProcessHostedService>();

        RegisterRabbitMqConsumeInboxEventBusMessageHostedService(serviceCollection);
        RegisterRabbitMqSendOutboxEventBusMessageHostedService(serviceCollection);
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        ServiceProvider.GetRequiredService<PlatformRabbitMqProcessInitializerService>().StartProcess(default);
    }

    protected abstract PlatformRabbitMqOptions RabbitMqOptionsFactory(IServiceProvider serviceProvider);

    protected virtual void RegisterRabbitMqConsumeInboxEventBusMessageHostedService(IServiceCollection serviceCollection)
    {
        serviceCollection.RemoveIfExist(PlatformConsumeInboxBusMessageHostedService.MatchImplementation);
        serviceCollection.RegisterHostedService<PlatformRabbitMqConsumeInboxBusMessageHostedService>();
    }

    protected virtual void RegisterRabbitMqSendOutboxEventBusMessageHostedService(IServiceCollection serviceCollection)
    {
        serviceCollection.RemoveIfExist(PlatformSendOutboxBusMessageHostedService.MatchImplementation);
        serviceCollection.RegisterHostedService<PlatformRabbitMqSendOutboxBusMessageHostedService>();
    }
}
