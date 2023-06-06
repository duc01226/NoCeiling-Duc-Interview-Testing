namespace Easy.Platform.RabbitMQ;

public class PlatformProducerRabbitMqChannelPool : PlatformRabbitMqChannelPool
{
    public PlatformProducerRabbitMqChannelPool(PlatformRabbitMqChannelPoolPolicy channelPoolPolicy) : base(channelPoolPolicy)
    {
    }
}
