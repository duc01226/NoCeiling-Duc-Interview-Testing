using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

public class PlatformRabbitMqChannelPoolPolicy : IPooledObjectPolicy<IModel>, IDisposable
{
    public const int TryWaitGetConnectionSeconds = 120;

    private readonly PlatformRabbitMqOptions options;
    private bool disposed;
    private RabbitMqConnectionPool rabbitMqConnectionPool;

    public PlatformRabbitMqChannelPoolPolicy(
        int poolSize,
        int reuseChannelPerConnectionCount,
        PlatformRabbitMqOptions options)
    {
        this.options = options;
        ReuseChannelPerConnectionCount = reuseChannelPerConnectionCount;
        rabbitMqConnectionPool = new RabbitMqConnectionPool(options, poolSize >= ReuseChannelPerConnectionCount ? poolSize / ReuseChannelPerConnectionCount : 1);
    }

    public int ReuseChannelPerConnectionCount { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IModel Create()
    {
        var connection = rabbitMqConnectionPool.TryWaitGetConnection(TryWaitGetConnectionSeconds);

        try
        {
            var channel = connection.CreateModel();

            // Config the prefectCount. "defines the max number of unacknowledged deliveries that are permitted on a channel" to limit messages to prevent rabbit mq down
            // Reference: https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html. Filter: BasicQos
            channel.BasicQos(prefetchSize: 0, options.QueuePrefetchCount, false);

            return channel;
        }
        finally
        {
            rabbitMqConnectionPool.ReturnConnection(connection);
        }
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
        if (!disposed)
        {
            // Release managed resources
            if (disposing)
            {
                rabbitMqConnectionPool?.Dispose();
                rabbitMqConnectionPool = default;
            }

            // Release unmanaged resources

            disposed = true;
        }
    }
}
