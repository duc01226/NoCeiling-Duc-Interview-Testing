namespace Easy.Platform.RabbitMQ;

public class PlatformProducerRabbitMqChannelPool : PlatformRabbitMqChannelPool
{
    public PlatformProducerRabbitMqChannelPool(PlatformRabbitMqOptions options) : base(
        new PlatformRabbitMqChannelPoolPolicy(
            options.ProducerChannelPoolSize,
            options))
    {
    }
}
