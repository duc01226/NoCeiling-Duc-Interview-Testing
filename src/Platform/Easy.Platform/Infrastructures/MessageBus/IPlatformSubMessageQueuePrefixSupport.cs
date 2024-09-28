namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformSubMessageQueuePrefixSupport : IPlatformMessage
{
    /// <summary>
    /// Default can return null or empty. When return null mean that no sub-queue based on message content defined => mean that they all can process independently without blocking each other if any same consumer processing or failed messages. <br></br>
    /// If return a constant same value (Not null or empty) => all messages will be processed in queue order FIFO because all message in the same "constant value" sub-queue. <br></br>
    /// SubQueuePrefix Value will be used to group message with same consumer or message-type for producer into group by SubQueuePrefix. <br></br>
    /// It helps to allow message with different SubQueuePrefix could run in parallel.
    /// Message with same sub-queue prefix value in same consumer or message type for producer should process in queue FIFO, which failed message will block later new message to be processed <br></br>
    /// Message with same SubQueuePrefix will be processing in queue, first in first out. Any message failed will stop processing other later messages.
    /// </summary>
    public string? SubQueuePrefix();
}
