namespace Easy.Platform.RabbitMQ;

public class PlatformConsumerRabbitMqChannelPool : PlatformRabbitMqChannelPool
{
    public PlatformConsumerRabbitMqChannelPool(
        PlatformRabbitMqOptions options) : base(new PlatformRabbitMqChannelPoolPolicy(options.CalculateNumberOfParallelConsumers(), options))
    {
        MaximumRetained = options.CalculateNumberOfParallelConsumers();
    }
}
