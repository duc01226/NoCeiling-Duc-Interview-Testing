namespace Easy.Platform.RabbitMQ;

public static class NumberOfParallelConsumersPerQueue
{
    public static readonly int Value = Environment.ProcessorCount;
}
