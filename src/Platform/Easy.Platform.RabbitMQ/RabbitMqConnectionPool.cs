using System.Reflection;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

public class RabbitMqConnectionPool
{
    private readonly DefaultObjectPool<IConnection> connectionPool;

    public RabbitMqConnectionPool(PlatformRabbitMqOptions options, int poolSize)
    {
        connectionPool = new DefaultObjectPool<IConnection>(
            new RabbitMqPooledObjectPolicy(options),
            poolSize
        );
    }

    public IConnection GetConnection()
    {
        return connectionPool.Get();
    }

    public void ReturnConnection(IConnection connection)
    {
        connectionPool.Return(connection);
    }
}

public class RabbitMqPooledObjectPolicy : IPooledObjectPolicy<IConnection>
{
    private readonly IConnectionFactory connectionFactory;
    private readonly PlatformRabbitMqOptions options;

    public RabbitMqPooledObjectPolicy(PlatformRabbitMqOptions options)
    {
        this.options = options;
        connectionFactory = InitializeFactory();
    }

    public IConnection Create()
    {
        // Store stack trace before call CreateConnection to keep the original stack trace to log
        // after CreateConnection will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = Environment.StackTrace;

        try
        {
            return CreateConnection();
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"{GetType().Name} CreateConnection failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                ex);
        }
    }

    public bool Return(IConnection obj)
    {
        if (obj.IsOpen) return true;
        obj.Dispose();
        return false;
    }

    private IConnection CreateConnection()
    {
        // Store stack trace before call CreateConnection to keep the original stack trace to log
        // after CreateConnection will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = Environment.StackTrace;

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
            SocketWriteTimeout = options.SocketTimeoutSeconds.Seconds(),
            RequestedChannelMax = options.RequestedChannelMax
        };

        return connectionFactoryResult;
    }
}
