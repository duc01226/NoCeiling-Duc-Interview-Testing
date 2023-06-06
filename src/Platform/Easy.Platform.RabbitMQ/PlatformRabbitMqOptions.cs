using Easy.Platform.Infrastructures.MessageBus;
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
    public int InitRabbitMqChannelRetryCount { get; set; } = 30;

    /// <summary>
    /// Config the prefectCount. "defines the max number of unacknowledged deliveries that are permitted on a channel" to limit messages to prevent rabbit mq down
    /// Reference: https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html. Filter: BasicQos
    /// </summary>
    public ushort QueuePrefetchCount { get; set; } = 100;

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

    /// <summary>
    /// Config the time to true to log consumer process time
    /// </summary>
    public bool EnableLogConsumerProcessTime { get; set; } = false;

    /// <summary>
    /// Config the time in milliseconds to log warning if the process consumer time is over LogSlowProcessingConsumerWarningMilliseconds.
    /// </summary>
    public long LogSlowProcessingConsumerWarningMilliseconds { get; set; } = PlatformMessageBusConsumer.DefaultProcessWarningTimeMilliseconds;

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
}
