namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformSubMessageQueuePrefixSupport : IPlatformMessage
{
    /// <summary>
    /// Default can return null. When return null mean that no sub-queue based on message content defined => all messages need to be processed in queue FIFO,
    /// the failed message will stop processing new messages in the same consumer queue because all message in the same "null" sub-queue. <br></br>
    /// If you want all messages have it's own unique sub-queue, mean that they all can process independently without blocking each other if failed messages, should return a unique string like Guid, example return TrackId. <br></br>
    /// SubQueuePrefix Value will be used to group message with same consumer or message-type for producer into group by SubQueuePrefix. <br></br>
    /// It helps to allow message with different SubQueuePrefix could run in parallel.
    /// Message with same sub-queue prefix value in same consumer or message type for producer should process in queue FIFO, which failed message will block later new message to be processed <br></br>
    /// Message with same SubQueuePrefix will be processing in queue, first in first out. Any message failed will stop processing other later messages.
    /// </summary>
    public string? SubQueuePrefix();
}
