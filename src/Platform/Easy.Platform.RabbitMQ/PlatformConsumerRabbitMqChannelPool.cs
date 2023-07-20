namespace Easy.Platform.RabbitMQ;

public class PlatformConsumerRabbitMqChannelPool : PlatformRabbitMqChannelPool
{
    public PlatformConsumerRabbitMqChannelPool(
        PlatformRabbitMqChannelPoolPolicy channelPoolPolicy,
        PlatformRabbitMqOptions rabbitMqOptions) : base(
        channelPoolPolicy)
    {
        MaximumRetained = rabbitMqOptions.CalculateNumberOfParallelConsumers();
    }
}
