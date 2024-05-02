namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformSubMessageQueuePrefixSupport : IPlatformMessage
{
    /// <summary>
    /// Default can return null. If return not null, value will be used to group message with same consumer or message-type for producer into group by SubQueuePrefix.
    /// It help to define like "sub-queue" to allow message with different SubQueuePrefix could run in parallel.
    /// Message in the same "sub-queue" with same SubQueuePrefix will run in queued.
    /// Message with same SubQueuePrefix will be processing in queue, first in first out. Any message failed will stop processing other later messages.
    /// </summary>
    public string? SubQueuePrefix();
}
