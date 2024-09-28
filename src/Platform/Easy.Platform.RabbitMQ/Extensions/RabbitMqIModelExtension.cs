using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ.Extensions;

public static class RabbitMqIModelExtension
{
    public static bool IsClosedPermanently(this IModel channel)
    {
        try
        {
            // Only if the close reason is shutdown, the server might just shutdown temporarily, so we still try to keep the channel for retry connect later
            return channel.IsClosed && channel.CloseReason != null && channel.CloseReason.ReplyCode != RabbitMqCloseReasonCodes.ServerShutdown;
        }
        catch (ObjectDisposedException e)
        {
            return true;
        }
    }
}
