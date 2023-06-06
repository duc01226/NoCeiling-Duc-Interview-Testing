using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

public class PlatformRabbitMqChannelPoolPolicy : IPooledObjectPolicy<IModel>, IDisposable
{
    private readonly IConnectionFactory connectionFactory;

    private Lazy<IConnection> connectionInitializer;
    private readonly ILogger<PlatformRabbitMqChannelPoolPolicy> logger;
    private readonly PlatformRabbitMqOptions options;

    public PlatformRabbitMqChannelPoolPolicy(
        PlatformRabbitMqOptions options,
        ILogger<PlatformRabbitMqChannelPoolPolicy> logger)
    {
        this.options = options;
        this.logger = logger;

        connectionFactory = InitializeFactory();
        connectionInitializer = new Lazy<IConnection>(CreateConnection);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IModel Create()
    {
        try
        {
            var channel = connectionInitializer.Value.CreateModel();

            // Config the prefectCount. "defines the max number of unacknowledged deliveries that are permitted on a channel" to limit messages to prevent rabbit mq down
            // Reference: https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html. Filter: BasicQos
            // QueuePrefetchCount : Default 1 to apply "Fair Dispatch"
            channel.BasicQos(prefetchSize: 0, (ushort)(options.QueuePrefetchCount / NumberOfParallelConsumersPerQueue.Value), false);

            return channel;
        }
        catch (Exception)
        {
            ReInitNewConnectionInitializer();
            throw;
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

    /// <summary>
    /// Connection hang up during broker node restarted
    /// in this case, try to close old and create new connection
    /// </summary>
    private void ReInitNewConnectionInitializer()
    {
        try
        {
            connectionInitializer.Value.Close();
            connectionInitializer.Value.Dispose();
        }
        catch (Exception releaseEx)
        {
            logger.LogDebug(releaseEx, "Release rabbit-mq old connection failed.");
        }
        finally
        {
            connectionInitializer = new Lazy<IConnection>(CreateConnection);
        }
    }

    private IConnectionFactory InitializeFactory()
    {
        var connectionFactoryResult = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true, //https://www.rabbitmq.com/dotnet-api-guide.html#recovery
            NetworkRecoveryInterval = options.NetworkRecoveryIntervalSeconds.Seconds(),
            UserName = options.Username,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            Port = options.Port,
            DispatchConsumersAsync = true,
            ClientProvidedName = options.ClientProvidedName ?? Assembly.GetEntryAssembly()?.FullName,
            RequestedConnectionTimeout = options.RequestedConnectionTimeoutSeconds.Seconds(),
            ContinuationTimeout = options.RequestedConnectionTimeoutSeconds.Seconds(),
            SocketReadTimeout = options.SocketTimeoutSeconds.Seconds(),
            SocketWriteTimeout = options.SocketTimeoutSeconds.Seconds()
        };

        return connectionFactoryResult;
    }

    private IConnection CreateConnection()
    {
        // Store stack trace before call CreateConnection to keep the original stack trace to log
        // after CreateConnection will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = Environment.StackTrace;

        return Util.TaskRunner.WaitRetryThrowFinalException(
            () =>
            {
                try
                {
                    var hostNames = options.HostNames.Split(',')
                        .Where(hostName => hostName.IsNotNullOrEmpty())
                        .ToArray();

                    return connectionFactory.CreateConnection(hostNames);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"{GetType().Name} CreateConnection failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                        ex);
                }
            },
            retryAttempt => 1.Seconds(),
            retryCount: 10);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
