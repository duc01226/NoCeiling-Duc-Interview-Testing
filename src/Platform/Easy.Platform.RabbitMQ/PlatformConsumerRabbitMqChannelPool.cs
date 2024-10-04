using Easy.Platform.Infrastructures.MessageBus;

namespace Easy.Platform.RabbitMQ;

public class PlatformConsumerRabbitMqChannelPool : PlatformRabbitMqChannelPool
{
    public PlatformConsumerRabbitMqChannelPool(PlatformRabbitMqOptions options, IPlatformMessageBusScanner messageBusScanner) : base(
        new PlatformRabbitMqChannelPoolPolicy(
            messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys().Count,
            options))
    {
    }
}
