using System.Reflection;
using Easy.Platform.Common;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

public class RabbitMqConnectionPool : IDisposable
{
    private readonly DefaultObjectPool<IConnection> connectionPool;
    private readonly Lock currentUsingObjectCounterLock = new();
    private bool disposed;

    public RabbitMqConnectionPool(PlatformRabbitMqOptions options, int poolSize)
    {
        PoolSize = poolSize;
        connectionPool = new DefaultObjectPool<IConnection>(
            new RabbitMqPooledObjectPolicy(options),
            poolSize
        );
    }

    protected int PoolSize { get; }

    public int CurrentUsingObjectCounter { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IConnection GetConnection()
    {
        if (IsConnectionPoolFull()) throw new Exception($"RabbitMqConnectionPool is fulled. PoolSize is {PoolSize}");

        var result = ProcessGetConnectionFromPool();

        return result;
    }


    public IConnection TryWaitGetConnection(
        int maxWaitSeconds)
    {
        return Util.TaskRunner.WaitUntilGetValidResultAsync(
                this,
                _ =>
                {
                    if (IsConnectionPoolFull()) return null;

                    var result = ProcessGetConnectionFromPool();

                    return result;
                },
                con => con != null,
                maxWaitSeconds,
                delayRetryTimeSeconds: 1,
                waitForMsg: $"RabbitMqConnectionPool is fulled. PoolSize is {PoolSize}")
            .GetResult();
    }

    public void ReturnConnection(IConnection connection)
    {
        lock (currentUsingObjectCounterLock)
        {
            if (CurrentUsingObjectCounter > 0)
                CurrentUsingObjectCounter--;

            connectionPool.Return(connection);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Release managed resources
            }

            // Release unmanaged resources

            disposed = true;
        }
    }

    private IConnection ProcessGetConnectionFromPool()
    {
        lock (currentUsingObjectCounterLock)
        {
            var result = connectionPool.Get();

            CurrentUsingObjectCounter++;

            return result;
        }
    }

    private bool IsConnectionPoolFull()
    {
        return CurrentUsingObjectCounter >= PoolSize;
    }

    ~RabbitMqConnectionPool()
    {
        Dispose(false);
    }
}

public class RabbitMqPooledObjectPolicy : IPooledObjectPolicy<IConnection>
{
    private readonly ConnectionFactory connectionFactory;
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
        var fullStackTrace = PlatformEnvironment.StackTrace();

        try
        {
            return CreateConnection().GetResult();
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

    private async Task<IConnection> CreateConnection()
    {
        // Store stack trace before call CreateConnection to keep the original stack trace to log
        // after CreateConnection will lose full stack trace (may because it connect async to other external service)
        var fullStackTrace = PlatformEnvironment.StackTrace();

        try
        {
            var hostNames = options.HostNames.Split(',')
                .Where(hostName => hostName.IsNotNullOrEmpty())
                .ToArray();

            return await connectionFactory.CreateConnectionAsync(hostNames);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"{GetType().Name} CreateConnection failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                ex);
        }
    }

    private ConnectionFactory InitializeFactory()
    {
        var connectionFactoryResult = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true, //https://www.rabbitmq.com/dotnet-api-guide.html#recovery
            NetworkRecoveryInterval = options.NetworkRecoveryIntervalSeconds.Seconds(),
            UserName = options.Username,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            Port = options.Port,
            ConsumerDispatchConcurrency = (ushort)Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent,
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
