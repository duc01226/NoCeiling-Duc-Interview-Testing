using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// Represents the configuration options for RabbitMQ in the platform.
/// </summary>
public class PlatformRabbitMqOptions
{
    /// <summary>
    /// 180 like 180 seconds. Retry delay time default is 1 second
    /// </summary>
    public const int DefaultInitRabbitMqChannelRetryCount = 180;

    /// <summary>
    /// Gets or sets the host names for the RabbitMQ server. This can be a comma-separated list for clustered environments.
    /// </summary>
    public string HostNames { get; set; }

    /// <summary>
    /// Gets or sets the username used for authenticating with the RabbitMQ server.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password used for authenticating with the RabbitMQ server.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the port number for the RabbitMQ server. Defaults to <see cref="AmqpTcpEndpoint.UseDefaultPort" />.
    /// </summary>
    public int Port { get; set; } = AmqpTcpEndpoint.UseDefaultPort;

    /// <summary>
    /// Gets or sets the virtual host to use on the RabbitMQ server. Defaults to "/", the default virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets a client-provided name for the RabbitMQ connection. This can be useful for identifying connections in monitoring tools.
    /// </summary>
    public string ClientProvidedName { get; set; }

    /// <summary>
    /// Gets or sets the number of times to retry initializing a RabbitMQ channel (<see cref="IModel" />) if the initial attempt fails.
    /// This ensures resilience against transient network issues during connection establishment.
    /// </summary>
    public int InitRabbitMqChannelRetryCount { get; set; } = DefaultInitRabbitMqChannelRetryCount;

    /// <summary>
    /// Gets the queue prefetch count, which defines the maximum number of unacknowledged deliveries allowed on a single channel.
    /// This value is crucial for preventing message queue overload and ensuring smooth message processing.
    /// <br />
    /// The prefetch count is dynamically calculated based on the number of parallel consumers and aims to distribute messages evenly for optimal parallel processing.
    /// <br />
    /// References:
    /// <br />
    /// - RabbitMQ BasicQos: <see href="https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html" />
    /// <br />
    /// - Setting the Correct Prefetch Value: <see href="https://www.cloudamqp.com/blog/part1-rabbitmq-best-practice.html#how-to-set-correct-prefetch-value" />
    /// </summary>
    public ushort QueuePrefetchCount { get; set; } = (ushort)Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent;

    /// <summary>
    /// Gets or sets the interval in seconds for network recovery attempts.
    /// This setting controls how often the RabbitMQ client attempts to reconnect if the connection to the server is lost.
    /// <br />
    /// This value is used to set the <see cref="ConnectionFactory.NetworkRecoveryInterval" /> property of the RabbitMQ client.
    /// </summary>
    public int NetworkRecoveryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the requested connection timeout in seconds.
    /// This value determines how long the client waits for a connection to the RabbitMQ server to be established.
    /// <br />
    /// This value is used to set both the <see cref="ConnectionFactory.RequestedConnectionTimeout" /> and <see cref="ConnectionFactory.ContinuationTimeout" /> properties of the RabbitMQ client.
    /// </summary>
    public int RequestedConnectionTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the socket timeout in seconds for both read and write operations.
    /// This value sets the timeout for reading from or writing to the underlying network socket used for communication with the RabbitMQ server.
    /// <br />
    /// This value is used to set both the <see cref="ConnectionFactory.SocketReadTimeout" /> and <see cref="ConnectionFactory.SocketWriteTimeout" /> properties of the RabbitMQ client.
    /// </summary>
    public int SocketTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the maximum number of channels allowed on a single connection.
    /// Channels are lightweight virtual connections that multiplex over a single TCP connection, and this setting limits the number of channels that can be opened concurrently.
    /// </summary>
    public ushort RequestedChannelMax { get; set; } = ushort.MaxValue;

    /// <summary>
    /// Gets or sets the delay time in seconds for requeueing a message.
    /// This delay is applied when a message needs to be requeued for later processing, for example, due to a temporary failure.
    /// </summary>
    public double RequeueDelayTimeInSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the expiration time in seconds for requeued messages.
    /// If a message remains in the requeue state for longer than this duration, it is considered expired and might be discarded or handled differently.
    /// </summary>
    public double RequeueExpiredInSeconds { get; set; } = TimeSpan.FromDays(7).TotalSeconds;

    /// <summary>
    /// Gets or sets the delay in seconds between retries when processing a requeued message fails.
    /// This delay provides a backoff mechanism, preventing the system from overwhelming itself with retries if a message repeatedly fails to process.
    /// </summary>
    public int ProcessRequeueMessageRetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of attempts to process a requeued message before considering it permanently failed.
    /// This setting prevents infinite retry loops for messages that encounter persistent errors during processing.
    /// </summary>
    public int ProcessRequeueMessageRetryCount { get; set; } = 3600 * 24 / 30;

    /// <summary>
    /// Gets or sets the default time-to-live (TTL) for messages in a queue, specified in milliseconds.
    /// This setting ensures that messages don't remain in the queue indefinitely, especially if they are not consumed due to errors or other issues.
    /// <br />
    /// After this time, the message will be automatically removed from the queue.
    /// <br />
    /// References:
    /// <br />
    /// - RabbitMQ TTL: <see href="https://www.rabbitmq.com/ttl.html" />
    /// </summary>
    public int QueueMessagesTimeToLive { get; set; } = 7 * 24 * 3600 * 1000;

    /// <summary>
    /// Gets or sets the expiration time for unused queues, specified in milliseconds.
    /// If a queue remains unused (no consumers) for this duration, it will be automatically deleted.
    /// <br />
    /// This feature can be used in conjunction with the auto-delete queue property.
    /// </summary>
    public int QueueUnusedExpireTime { get; set; } = 3 * 24 * 3600 * 1000;

    /// <summary>
    /// Gets or sets the maximum number of messages that can be stored in memory for a queue.
    /// This setting helps manage memory consumption by persisting messages to disk when the limit is reached.
    /// <br />
    /// This is a best practice to prevent excessive memory usage by RabbitMQ, especially for high-throughput scenarios.
    /// <br />
    /// References:
    /// <br />
    /// - RabbitMQ Lazy Queues: <see href="https://www.rabbitmq.com/lazy-queues.html" />
    /// </summary>
    public int QueueMaxNumberMessagesInMemory { get; set; } = 100;

    /// <summary>
    /// Gets or sets the size of the channel pool for producers.
    /// A larger pool allows for more concurrent message publishing operations.
    /// </summary>
    public int ProducerChannelPoolSize { get; set; } = Util.TaskRunner.DefaultParallelComputeTaskMaxConcurrent;

    /// <summary>
    /// Gets or sets the number of times a consumer channel can be reused within a single connection.
    /// Reusing channels reduces the overhead of creating new channels for each message consumption operation.
    /// </summary>
    public int ConsumerReuseChannelPerConnectionCount { get; set; } = 4;

    /// <summary>
    /// Gets or sets the number of times a producer channel can be reused within a single connection.
    /// Reusing channels reduces the overhead of creating new channels for each message publishing operation.
    /// </summary>
    public int ProducerReuseChannelPerConnectionCount { get; set; } = 4;

    public int MinimumRetryTimesToLogError { get; set; } = DefaultInitRabbitMqChannelRetryCount / 2;

    public int MaxConcurrentProcessing { get; set; } = Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent;
}
