using Easy.Platform.Infrastructures.MessageBus;

namespace Easy.Platform.RabbitMQ;

public class PlatformConsumerRabbitMqChannelPool : PlatformRabbitMqChannelPool
{
    public PlatformConsumerRabbitMqChannelPool(PlatformRabbitMqChannelPoolPolicy channelPoolPolicy, IPlatformMessageBusScanner messageBusScanner) : base(
        channelPoolPolicy)
    {
        // MaximumRetained equal all consumers count so that each consumer could use one different channel => handling parallel message for each consumer
        MaximumRetained = messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys().Count * NumberOfParallelConsumersPerQueue.Value;
    }
}
