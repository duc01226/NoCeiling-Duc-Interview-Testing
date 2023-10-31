using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

public class PlatformRabbitMqChannelPoolPolicy : IPooledObjectPolicy<IModel>, IDisposable
{
    private readonly PlatformRabbitMqOptions options;
    private readonly RabbitMqConnectionPool rabbitMqConnectionPool;

    public PlatformRabbitMqChannelPoolPolicy(
        int poolSize,
        PlatformRabbitMqOptions options)
    {
        this.options = options;
        rabbitMqConnectionPool = new RabbitMqConnectionPool(options, poolSize);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IModel Create()
    {
        var connection = rabbitMqConnectionPool.GetConnection();

        var channel = connection.CreateModel();

        // Config the prefectCount. "defines the max number of unacknowledged deliveries that are permitted on a channel" to limit messages to prevent rabbit mq down
        // Reference: https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html. Filter: BasicQos
        channel.BasicQos(prefetchSize: 0, options.QueuePrefetchCount, false);

        rabbitMqConnectionPool.ReturnConnection(connection);

        return channel;
    }

    public bool Return(IModel obj)
    {
        // Only if the close reason is shutdown, the server might just shutdown temporarily, so we still try to keep the channel for retry connect later
        if (obj.CloseReason != null && obj.CloseReason.ReplyCode != RabbitMqCloseReasonCodes.ServerShutdown)
        {
            obj.Dispose();
            return false;
        }

        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
