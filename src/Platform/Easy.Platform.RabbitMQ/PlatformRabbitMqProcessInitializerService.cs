using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Easy.Platform.Application.Context;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Timing;
using Easy.Platform.Infrastructures.MessageBus;
using Easy.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// This service main purpose is to configure RabbitMq Exchange, Declare Queue for each Consumer based on Consumer
/// Name/Consumer Message Name via RoutingKey.
/// Then start to connect listening messages, execute consumer which handle the suitable message
/// </summary>
public class PlatformRabbitMqProcessInitializerService : IDisposable
{
    public const int AckMessageRetryCount = int.MaxValue;
    public const int AckMessageRetryDelaySeconds = 10;
    public static readonly ActivitySource ActivitySource = new(nameof(PlatformRabbitMqProcessInitializerService));
    public static readonly TextMapPropagator TracingActivityPropagator = Propagators.DefaultTextMapPropagator;
    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly PlatformConsumerRabbitMqChannelPool channelPool;

    private CancellationToken currentStartProcessCancellationToken;
    private readonly IPlatformRabbitMqExchangeProvider exchangeProvider;
    private readonly IPlatformMessageBusScanner messageBusScanner;
    private readonly PlatformRabbitMqOptions options;
    private bool processStarted;
    private readonly ConcurrentDictionary<string, List<Type>> routingKeyToCanProcessConsumerTypesCacheMap = new();

    private readonly IServiceProvider serviceProvider;

    // Because connect consumer could lead to timeout exception if call parallel connect a lot, so that lock to just connect once at a time
    private readonly SemaphoreSlim startConnectSingleConsumerLock = new(initialCount: 1, maxCount: 1);
    private readonly SemaphoreSlim startProcessLock = new(initialCount: 1, maxCount: 1);
    private readonly SemaphoreSlim stopProcessLock = new(initialCount: 1, maxCount: 1);
    private readonly ConcurrentDictionary<string, object> waitingAckMessages = new();

    public PlatformRabbitMqProcessInitializerService(
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformRabbitMqExchangeProvider exchangeProvider,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformConsumerRabbitMqChannelPool channelPool,
        PlatformRabbitMqOptions options,
        IServiceProvider serviceProvider)
    {
        this.applicationSettingContext = applicationSettingContext;
        this.exchangeProvider = exchangeProvider;
        this.messageBusScanner = messageBusScanner;
        this.channelPool = channelPool;
        this.options = options;
        this.serviceProvider = serviceProvider;
        Logger = PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformRabbitMqProcessInitializerService));
        InvokeConsumerLogger = PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformMessageBusConsumer));
    }

    protected ILogger Logger { get; }

    protected ILogger InvokeConsumerLogger { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task StartProcess(CancellationToken cancellationToken)
    {
        await startProcessLock.WaitAsync(cancellationToken);

        currentStartProcessCancellationToken = cancellationToken;

        try
        {
            if (processStarted) return;

            Logger.LogInformation($"[{GetType().Name}] RabbitMq init process STARTED");

            DeclareRabbitMqConfiguration();

            await StartConnectConsumersToQueues();

            await Task.WhenAll(
                PlatformGlobal.RootServiceProvider.GetRequiredService<PlatformSendOutboxBusMessageHostedService>().StartAsync(currentStartProcessCancellationToken),
                PlatformGlobal.RootServiceProvider.GetRequiredService<PlatformOutboxBusMessageCleanerHostedService>().StartAsync(currentStartProcessCancellationToken),
                PlatformGlobal.RootServiceProvider.GetRequiredService<PlatformConsumeInboxBusMessageHostedService>().StartAsync(currentStartProcessCancellationToken),
                PlatformGlobal.RootServiceProvider.GetRequiredService<PlatformInboxBusMessageCleanerHostedService>().StartAsync(currentStartProcessCancellationToken));

            processStarted = true;

            Logger.LogInformation($"[{GetType().Name}] RabbitMq init process FINISHED");
        }
        finally
        {
            //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
            //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
            startProcessLock.Release();
        }
    }

    public async Task StopProcess()
    {
        await stopProcessLock.WaitAsync(CancellationToken.None);

        try
        {
            if (!processStarted) return;

            waitingAckMessages.Clear();

            processStarted = false;
        }
        finally
        {
            //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
            //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
            stopProcessLock.Release();
        }
    }

    private void DeclareRabbitMqConfiguration()
    {
        InitRabbitMqChannel();

        // retryCount: messageBusManager.AllDefinedBusMessageAndConsumerBindingRoutingKeys().Count
        // to update the dictionary for queue that need to force delete to re-declare queue
        Util.TaskRunner.WaitRetryThrowFinalException(
            DeclareRabbitMqExchangesAndQueuesConfiguration,
            retryAttempt => TimeSpan.Zero,
            retryCount: messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys().Count * 3); // Count * 3 to ensure retry declare queue works for all queues
    }

    private async Task StartConnectConsumersToQueues()
    {
        try
        {
            await Task.Run(
                () => IPlatformModule.WaitAllModulesInitiated(typeof(IPlatformPersistenceModule), Logger, "to start connect all consumers to rabbitmq queue"),
                currentStartProcessCancellationToken);

            Logger.LogInformation("Start connect all consumers to rabbitmq queue STARTED");

            // Binding all defined event bus consumer to RabbitMQ Basic Consumer
            await messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys()
                .Select(GetConsumerQueueName)
                .ParallelAsync(
                    async queueName =>
                    {
                        // Support parallel handling messages for one queue. Each channel only can handling on thread, so that create each channel used for each consumer
                        // for one queue
                        await Enumerable.Range(0, NumberOfParallelConsumersPerQueue.Value)
                            .ParallelAsync(
                                async _ =>
                                {
                                    var currentChannel = channelPool.Get();

                                    // Config RabbitMQ Basic Consumer
                                    var applicationRabbitConsumer = new AsyncEventingBasicConsumer(currentChannel)
                                        .With(_ => _.Received += OnMessageReceived);

                                    // autoAck: false -> the Consumer will ack manually.
                                    startConnectSingleConsumerLock.ExecuteLockAction(
                                        () => currentChannel.BasicConsume(queueName, autoAck: false, applicationRabbitConsumer));
                                });

                        Logger.LogInformation(message: $"Consumer connected to queue {queueName}");
                    });

            Logger.LogInformation("Start connect all consumers to rabbitmq queue FINISHED");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, $"[{GetType().FullName}] RabbitMq Consumer can't start");
            throw;
        }
    }

    private string GetConsumerQueueName(string consumerRoutingKey)
    {
        return $"[Platform][{applicationSettingContext.ApplicationName}]-{consumerRoutingKey}";
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs rabbitMqMessage)
    {
        await TransferMessageToAllMessageBusConsumers(sender.Cast<AsyncEventingBasicConsumer>().Model, rabbitMqMessage, currentStartProcessCancellationToken);
    }

    private async Task TransferMessageToAllMessageBusConsumers(IModel channel, BasicDeliverEventArgs rabbitMqMessage, CancellationToken cancellationToken)
    {
        try
        {
            var canProcessConsumerTypes = rabbitMqMessage.RoutingKey.Pipe(
                routingKey =>
                {
                    if (!routingKeyToCanProcessConsumerTypesCacheMap.ContainsKey(routingKey))
                        routingKeyToCanProcessConsumerTypesCacheMap.TryAdd(
                            rabbitMqMessage.RoutingKey,
                            GetCanProcessConsumerTypes(routingKey));

                    return routingKeyToCanProcessConsumerTypesCacheMap[routingKey];
                });

            await canProcessConsumerTypes
                .ParallelAsync(
                    async consumerType =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        var parentContext = TracingActivityPropagator.Extract(
                            default,
                            rabbitMqMessage.BasicProperties,
                            ExtractTraceContextFromBasicProperties);

                        using (var activity = ActivitySource.StartActivity(
                            nameof(ExecuteConsumer),
                            ActivityKind.Consumer,
                            parentContext.ActivityContext))
                        {
                            using (var scope = serviceProvider.CreateScope())
                            {
                                var consumer = scope.ServiceProvider.GetService(consumerType).Cast<IPlatformMessageBusConsumer>();

                                if (consumer != null)
                                    await ExecuteConsumer(rabbitMqMessage, consumer, activity);
                            }
                        }
                    });

            AckMessage(channel, rabbitMqMessage, isReject: false);
        }
        catch (PlatformInvokeConsumerException ex)
        {
            Logger.LogError(
                ex,
                $"[MessageBus] Consume message error. [RoutingKey:{rabbitMqMessage.RoutingKey}].{Environment.NewLine}" +
                $"Message: {Encoding.UTF8.GetString(rabbitMqMessage.Body.Span)}");

            if (ProcessRequeueMessage(channel, rabbitMqMessage, ex.BusMessage) == false) AckMessage(channel, rabbitMqMessage, isReject: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                $"[MessageBus] Consume message error must REJECT. [RoutingKey:{rabbitMqMessage.RoutingKey}].{Environment.NewLine}" +
                $"Message: {Encoding.UTF8.GetString(rabbitMqMessage.Body.Span)}");

            // Reject the message.
            Util.TaskRunner.WaitRetry(
                () => channel.BasicReject(rabbitMqMessage.DeliveryTag, false),
                retryAttempt => 10.Seconds(),
                retryCount: 10);
        }
    }

    public void AckMessage(IModel channel, BasicDeliverEventArgs rabbitMqMessage, bool isReject)
    {
        // WHY: After consumed message successfully, ack the message is handled to rabbitmq so that message could be removed.
        Util.TaskRunner.QueueActionInBackground(
            () =>
            {
                waitingAckMessages.TryAdd(GetWaitingAckMessageKey(rabbitMqMessage, channel), null);

                Util.TaskRunner.WaitRetryThrowFinalException(
                    () =>
                    {
                        // Check CurrentChannel != null because CurrentChannel could be null when process restart
                        // because some reason the rabbitmq server restart
                        if (waitingAckMessages.ContainsKey(GetWaitingAckMessageKey(rabbitMqMessage, channel)))
                        {
                            if (isReject)
                                channel.BasicReject(rabbitMqMessage.DeliveryTag, false);
                            else
                                channel.BasicAck(rabbitMqMessage.DeliveryTag, false);
                        }

                        waitingAckMessages.TryRemove(GetWaitingAckMessageKey(rabbitMqMessage, channel), out _);
                    },
                    retryAttempt => TimeSpan.FromSeconds(AckMessageRetryDelaySeconds),
                    retryCount: AckMessageRetryCount,
                    ex => Logger.LogError(
                        ex,
                        "[MessageBus] Failed to ack the message. RoutingKey:{RoutingKey}. DeliveryTag:{DeliveryTag}",
                        rabbitMqMessage.RoutingKey,
                        rabbitMqMessage.DeliveryTag));
            },
            () => Logger,
            cancellationToken: CancellationToken.None);
    }

    private List<Type> GetCanProcessConsumerTypes(string messageRoutingKey)
    {
        return messageBusScanner.ScanAllDefinedConsumerTypes()
            .Where(
                messageBusConsumerType =>
                {
                    if (messageBusConsumerType.GetCustomAttributes<PlatformConsumerRoutingKeyAttribute>().IsEmpty())
                    {
                        var consumerGenericType = messageBusConsumerType.FindMatchedGenericType(typeof(IPlatformMessageBusConsumer<>));

                        var matchedDefaultMessageRoutingKey =
                            IPlatformMessageBusConsumer.BuildForConsumerDefaultBindingRoutingKey(consumerGenericType);

                        return matchedDefaultMessageRoutingKey.Match(messageRoutingKey);
                    }

                    return PlatformConsumerRoutingKeyAttribute.CanMessageBusConsumerProcess(
                        messageBusConsumerType,
                        messageRoutingKey);
                })
            .Select(
                consumerType => new
                {
                    ConsumerType = consumerType,
                    ConsumerExecuteOrder = serviceProvider.ExecuteScoped(
                        scope => scope.ServiceProvider.GetService(consumerType).Cast<IPlatformMessageBusConsumer>().ExecuteOrder())
                })
            .OrderBy(p => p.ConsumerExecuteOrder)
            .Select(p => p.ConsumerType)
            .ToList();
    }

    private static string GetWaitingAckMessageKey(BasicDeliverEventArgs rabbitMqMessage, IModel channel)
    {
        return $"{rabbitMqMessage.RoutingKey}_Channel:{channel.ChannelNumber}_{rabbitMqMessage.DeliveryTag}";
    }

    private bool ProcessRequeueMessage(IModel channel, BasicDeliverEventArgs rabbitMqMessage, object busMessage)
    {
        var messageCreatedDate = busMessage.As<IPlatformTrackableBusMessage>()?.CreatedUtcDate;
        if (options.RequeueExpiredInSeconds > 0 &&
            messageCreatedDate != null &&
            messageCreatedDate.Value.AddSeconds(options.RequeueExpiredInSeconds) < Clock.UtcNow)
            return false;

        // Requeue the message.
        // References: https://www.rabbitmq.com/confirms.html#consumer-nacks-requeue for WHY of multiple: true, requeue: true
        // Summary: requeue: true =>  the broker will requeue the delivery (or multiple deliveries, as will be explained shortly) with the specified delivery tag
        // Why multiple: true for Nack: to fix requeue true for multiple consumer instance by eject or requeue multiple messages at once.
        // Because if all consumers requeue because they cannot process a delivery due to a transient condition, they will create a requeue/redelivery loop. Such loops can be costly in terms of network bandwidth and CPU resources
        Util.TaskRunner.QueueActionInBackground(
            () =>
            {
                Util.TaskRunner.WaitRetryThrowFinalException(
                    () =>
                    {
                        channel.BasicNack(rabbitMqMessage.DeliveryTag, multiple: true, requeue: true);

                        Logger.LogWarning(
                            message: $"RabbitMQ retry queue message for the routing key: {rabbitMqMessage.RoutingKey}.{Environment.NewLine}" +
                                     "Message: {BusMessage}",
                            busMessage.ToJson());
                    },
                    retryAttempt => TimeSpan.FromSeconds(options.ProcessRequeueMessageRetryDelaySeconds),
                    retryCount: options.ProcessRequeueMessageRetryCount,
                    finalEx => Logger.LogError(
                        finalEx,
                        message: $"RabbitMQ retry queue failed message for the routing key: {rabbitMqMessage.RoutingKey}.{Environment.NewLine}" +
                                 "Message: {BusMessage}",
                        busMessage.ToJson()));
            },
            () => Logger,
            cancellationToken: currentStartProcessCancellationToken);

        return true;
    }

    /// <summary>
    /// Return Exception if failed to execute consumer
    /// </summary>
    private async Task ExecuteConsumer(BasicDeliverEventArgs args, IPlatformMessageBusConsumer consumer, Activity traceActivity = null)
    {
        // Get a generic type: PlatformMessageBusMessage<TMessage> where TMessage = TMessagePayload
        // of IPlatformMessageBusConsumer<TMessagePayload>
        var consumerMessageType = PlatformMessageBusConsumer.GetConsumerMessageType(consumer);

        var busMessage = Util.TaskRunner.CatchExceptionContinueThrow(
            () => PlatformJsonSerializer.Deserialize(
                args.Body.Span,
                consumerMessageType,
                consumer.CustomJsonSerializerOptions()),
            ex => Logger.LogError(
                ex,
                $"RabbitMQ parsing message to {consumerMessageType.Name} error for the routing key {args.RoutingKey}.{Environment.NewLine} Body: {Encoding.UTF8.GetString(args.Body.Span)}"));

        if (busMessage != null)
        {
            traceActivity?.SetTag("consumer", consumer.GetType().Name);
            traceActivity?.SetTag("message", busMessage.ToJson());

            await PlatformMessageBusConsumer.InvokeConsumerAsync(
                consumer,
                busMessage,
                args.RoutingKey,
                options.EnableLogConsumerProcessTime,
                options.LogSlowProcessingConsumerWarningMilliseconds,
                InvokeConsumerLogger);
        }
    }

    private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers != null && props.Headers.ContainsKey(key))
            {
                props.Headers.TryGetValue(key, out var value);

                return new[] { Encoding.UTF8.GetString(value.As<byte[]>() ?? Array.Empty<byte>()) };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract trace context");
        }

        return Enumerable.Empty<string>();
    }

    private void InitRabbitMqChannel()
    {
        Policy
            .Handle<Exception>()
            .WaitAndRetry(
                options.InitRabbitMqChannelRetryCount,
                retryAttempt => 10.Seconds())
            .ExecuteAndThrowFinalException(
                () => channelPool.TryInitFirstChannel(),
                ex =>
                {
                    Logger.LogError(ex, "Init rabbit-mq channel failed.");
                });
    }

    private void DeclareRabbitMqExchangesAndQueuesConfiguration()
    {
        // Get exchange routing key for all consumers in source code
        var allDefinedMessageBusConsumerPatternRoutingKeys = messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys();

        // Declare all exchanges
        DeclareExchangesForRoutingKeys(allDefinedMessageBusConsumerPatternRoutingKeys);
        // Declare all queues
        allDefinedMessageBusConsumerPatternRoutingKeys.ForEach(consumerRoutingKey => DeclareQueueForConsumer(consumerRoutingKey));
    }

    private void DeclareQueueForConsumer(PlatformBusMessageRoutingKey consumerBindingRoutingKey)
    {
        channelPool.GetChannelDoActionAndReturn(
            currentChannel =>
            {
                var exchange = GetConsumerExchange(consumerBindingRoutingKey);
                var queueName = GetConsumerQueueName(consumerBindingRoutingKey);

                try
                {
                    DeclareQueue(currentChannel, queueName);
                }
                catch (Exception)
                {
                    // If failed because queue is existing with different configurations/args, try to delete and declare again with new configuration
                    if (currentChannel.CloseReason?.ReplyCode == RabbitMqCloseReasonCodes.NotAcceptable)
                    {
                        try
                        {
                            channelPool.GetChannelDoActionAndReturn(p => p.QueueDelete(queueName, ifUnused: true, ifEmpty: true));
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(
                                e,
                                "Failed try to delete queue to declare new queue with updated configuration. If the queue still have messages, please process it or manually delete the queue. We still are using the old queue configuration");

                            // If delete queue failed, just ACCEPT using the current old one is OK
                            DeclareQueueBindForConsumer(currentChannel, consumerBindingRoutingKey, queueName, exchange);
                            return;
                        }

                        channelPool.GetChannelDoActionAndReturn(channel => DeclareQueue(channel, queueName));
                    }
                    else
                    {
                        throw;
                    }
                }

                DeclareQueueBindForConsumer(currentChannel, consumerBindingRoutingKey, queueName, exchange);
            });

        void DeclareQueue(IModel model, string queueName)
        {
            //*1
            // WHY: Set exclusive to false to support multiple consumers with the same type.
            // For example: in load balancing environment, we may have 2 instances of an API.
            // RabbitMQ will automatically apply load balancing behavior to send message to 1 instance only.

            // The "quorum" queue is a modern queue type for RabbitMQ implementing a durable, replicated FIFO queue based on the Raft consensus algorithm. https://www.rabbitmq.com/quorum-queues.html
            model.QueueDeclare(
                queueName,
                durable: true,
                exclusive: false, //*1
                autoDelete: false,
                arguments: Util.DictionaryBuilder.New<string, object>(
                    ("x-expires", options.QueueUnusedExpireTime),
                    ("x-queue-type", "quorum"),
                    ("message-ttl", options.QueueMessagesTimeToLive),
                    ("x-max-in-memory-length", options.QueueMaxNumberMessagesInMemory)));
        }
    }

    private void DeclareQueueBindForConsumer(
        IModel channel,
        string consumerBindingRoutingKey,
        string queueName,
        string exchange)
    {
        channel.QueueBind(queueName, exchange, consumerBindingRoutingKey);
        channel.QueueBind(
            queueName,
            exchange,
            $"{consumerBindingRoutingKey}.{PlatformRabbitMqConstants.FanoutBindingChar}");

        Logger.LogInformation(
            message:
            $"Queue {queueName} has been declared. Exchange:{exchange}. RoutingKey:{consumerBindingRoutingKey}");
    }

    private void DeclareExchangesForRoutingKeys(List<string> routingKeys)
    {
        routingKeys
            .GroupBy(p => exchangeProvider.GetExchangeName(p))
            .Select(p => p.Key)
            .ToList()
            .ForEach(
                exchangeName =>
                {
                    channelPool.GetChannelDoActionAndReturn(channel => channel.ExchangeDeclare(exchangeName, ExchangeType.Topic, durable: true));
                });
    }

    private string GetConsumerExchange(PlatformBusMessageRoutingKey consumerRoutingKey)
    {
        return exchangeProvider.GetExchangeName(consumerRoutingKey);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            startProcessLock?.Dispose();
            stopProcessLock?.Dispose();
        }
    }
}
