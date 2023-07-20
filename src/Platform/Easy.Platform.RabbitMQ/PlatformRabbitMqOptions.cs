using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

public class PlatformRabbitMqOptions
{
    public string HostNames { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public int Port { get; set; } = AmqpTcpEndpoint.UseDefaultPort;

    public string VirtualHost { get; set; } = "/";

    public string ClientProvidedName { get; set; }

    /// <summary>
    /// Used to set RetryCount policy when tried to create rabbit mq channel <see cref="IModel" />
    /// </summary>
    public int InitRabbitMqChannelRetryCount { get; set; } = 20;

    /// <summary>
    /// Config the prefectCount. "defines the max number of unacknowledged deliveries that are permitted on a channel" to limit messages to prevent rabbit mq down
    /// Reference: https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html. Filter: BasicQos
    /// QueuePrefetchCount : https://www.cloudamqp.com/blog/part1-rabbitmq-best-practice.html#how-to-set-correct-prefetch-value
    ///
    /// Default value to one to distribute message equally for parallel processing.
    /// If you have many consumers and/or long processing time, we recommend setting the prefetch count to one (1) so that messages are evenly distributed among all your workers.
    /// </summary>
    public ushort QueuePrefetchCount { get; set; } = 10;

    public int NumberOfParallelConsumersPerCpu { get; set; } = 10;

    public int MaxNumberOfParallelConsumers { get; set; } = 20;

    /// <summary>
    /// Used to set <see cref="ConnectionFactory.NetworkRecoveryInterval" />
    /// </summary>
    public int NetworkRecoveryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Used to set <see cref="ConnectionFactory.RequestedConnectionTimeout" /> and <see cref="ConnectionFactory.ContinuationTimeout" />
    /// </summary>
    public int RequestedConnectionTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Used to set <see cref="ConnectionFactory.SocketReadTimeout" /> and <see cref="ConnectionFactory.SocketWriteTimeout" />
    /// </summary>
    public int SocketTimeoutSeconds { get; set; } = 120;

    public ushort RequestedChannelMax { get; set; } = ushort.MaxValue;

    public double RequeueDelayTimeInSeconds { get; set; } = 60;

    public double RequeueExpiredInSeconds { get; set; } = TimeSpan.FromDays(7).TotalSeconds;

    public int ProcessRequeueMessageRetryDelaySeconds { get; set; } = 30;

    public int ProcessRequeueMessageRetryCount { get; set; } = 3600 * 24 / 30;

    /// <summary>
    /// References : https://www.rabbitmq.com/ttl.html.
    /// Set message default maximum time to live is one week. Usually if the message is not processed too long mean that it's obsoleted and should be discarded
    /// </summary>
    public int QueueMessagesTimeToLive { get; set; } = 7 * 24 * 3600 * 1000;

    /// <summary>
    /// Queues will expire after a period of time only when they are not used (e.g. do not have consumers). This feature can be used together with the auto-delete queue property. <br />
    /// Unit is milliseconds
    /// </summary>
    public int QueueUnusedExpireTime { get; set; } = 3 * 24 * 3600 * 1000;

    /// <summary>
    /// References: https://www.rabbitmq.com/lazy-queues.html. <br />
    /// For best practice, prevent queue too long in memory will store messages in the storage
    /// </summary>
    public int QueueMaxNumberMessagesInMemory { get; set; } = 100;

    public int CalculateNumberOfParallelConsumers()
    {
        return Math.Min(Environment.ProcessorCount * NumberOfParallelConsumersPerCpu, MaxNumberOfParallelConsumers);
    }
}
