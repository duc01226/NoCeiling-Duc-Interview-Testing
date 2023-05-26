namespace Easy.Platform.Infrastructures.MessageBus;

public sealed class PlatformMessageBusException<TMessage> : Exception
{
    public PlatformMessageBusException(TMessage eventBusMessage, Exception rootException) : base(
        rootException.Message,
        rootException)
    {
        EventBusMessage = eventBusMessage;
    }

    public TMessage EventBusMessage { get; }
}
