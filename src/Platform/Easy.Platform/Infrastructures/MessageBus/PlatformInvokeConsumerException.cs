namespace Easy.Platform.Infrastructures.MessageBus;

public sealed class PlatformInvokeConsumerException : Exception
{
    public PlatformInvokeConsumerException(
        Exception e,
        string consumerName,
        object busMessage) : base($"Invoke Consumer {consumerName} Failed.", e)
    {
        ConsumerName = consumerName;
        BusMessage = busMessage;
    }

    public string ConsumerName { get; }

    public object BusMessage { get; set; }
}
