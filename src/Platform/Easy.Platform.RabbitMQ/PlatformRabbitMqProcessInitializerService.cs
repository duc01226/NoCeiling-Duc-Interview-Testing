using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Easy.Platform.Application;
using Easy.Platform.Application.Context;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Timing;
using Easy.Platform.Infrastructures.MessageBus;
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
public sealed class PlatformRabbitMqProcessInitializerService
{
    public const int CheckToRestartProcessMaxFailedCounter = 100;
    public const int CheckToRestartProcessDelaySeconds = 6;
    public const int AckMessageRetryCount = CheckToRestartProcessMaxFailedCounter * 3;
    public const int AckMessageRetryDelaySeconds = 6;
    public static readonly ActivitySource ActivitySource = new(nameof(PlatformRabbitMqProcessInitializerService));
    public static readonly TextMapPropagator TracingActivityPropagator = Propagators.DefaultTextMapPropagator;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly object checkToRestartProcessRunningLock = new();
    private CancellationToken currentCancellationToken;

    private bool declareRabbitMqConfigurationFinished;
    private readonly IPlatformRabbitMqExchangeProvider exchangeProvider;
    private readonly HashSet<string> forceDeleteQueueBeforeDeclareQueues = new();
    private bool isCheckToRestartProcessIntervalRunning;
    private readonly IPlatformMessageBusScanner messageBusScanner;
    private readonly PlatformRabbitMqChannelPool mqChannelPool;
    private readonly PlatformRabbitMqOptions options;
    private bool processStarted;
    private readonly ConcurrentDictionary<string, List<Type>> routingKeyToCanProcessConsumerTypesCacheMap = new();
    private readonly IServiceProvider serviceProvider;
    private readonly object startProcessLock = new();
    private readonly object stopProcessLock = new();
    private readonly ConcurrentDictionary<string, object> waitingAckMessages = new();

    public PlatformRabbitMqProcessInitializerService(
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformRabbitMqExchangeProvider exchangeProvider,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformRabbitMqChannelPool mqChannelPool,
        PlatformRabbitMqOptions options,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        this.applicationSettingContext = applicationSettingContext;
        this.exchangeProvider = exchangeProvider;
        this.messageBusScanner = messageBusScanner;
        this.mqChannelPool = mqChannelPool;
        this.options = options;
        this.serviceProvider = serviceProvider;
        Logger = loggerFactory.CreateLogger<PlatformRabbitMqProcessInitializerService>();
    }

    private ILogger<PlatformRabbitMqProcessInitializerService> Logger { get; }

    public IModel CurrentChannel => mqChannelPool.GlobalChannel;

    public void StartProcess(CancellationToken cancellationToken)
    {
        lock (startProcessLock)
        {
            currentCancellationToken = cancellationToken;

            if (CurrentChannel != null && processStarted) return;

            while (DeclareRabbitMqConfiguration() == false)
                DeclareRabbitMqConfiguration();

            StartConsumers();

            Util.TaskRunner.QueueActionInBackground(
                () => StartCheckToRestartProcessInterval(cancellationToken),
                Logger,
                cancellationToken: cancellationToken);

            processStarted = true;

            Logger.LogInformation($"[{GetType().Name}] RabbitMq process has started successfully");
        }
    }

    public void RestartProcess(CancellationToken cancellationToken)
    {
        StopProcess();
        StartProcess(cancellationToken);
    }

    public async Task<bool> CheckShouldRestartProcess(CancellationToken cancellationToken)
    {
        lock (startProcessLock)
        {
            if (processStarted && !cancellationToken.IsCancellationRequested)
            {
                // If channel is closed, mean that it's no longer could be used => should restart immediately
                if (CurrentChannel == null || CurrentChannel.IsClosed) return true;

                for (var i = 1; i <= CheckToRestartProcessMaxFailedCounter; i++)
                {
                    if (CurrentChannel.IsClosed || !CurrentChannel.IsOpen)
                    {
                        Task.Delay(CheckToRestartProcessDelaySeconds.Seconds(), cancellationToken).WaitResult();

                        if (i == CheckToRestartProcessMaxFailedCounter && (CurrentChannel.IsClosed || !CurrentChannel.IsOpen)) return true;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Support restart rabbit-mq in-cases the rabbitmq server has been restarted, we need to re-init the channel
    /// </summary>
    public async Task StartCheckToRestartProcessInterval(CancellationToken cancellationToken)
    {
        lock (checkToRestartProcessRunningLock)
        {
            if (isCheckToRestartProcessIntervalRunning) return;

            isCheckToRestartProcessIntervalRunning = true;
        }

        while (isCheckToRestartProcessIntervalRunning && !cancellationToken.IsCancellationRequested)
        {
            if (await CheckShouldRestartProcess(cancellationToken))
                RestartProcess(cancellationToken);

            await Task.Delay(10.Seconds(), cancellationToken);
        }

        isCheckToRestartProcessIntervalRunning = false;
    }

    public async Task StopCheckToRestartProcessInterval()
    {
        lock (checkToRestartProcessRunningLock)
        {
            isCheckToRestartProcessIntervalRunning = false;
        }
    }

    public void StopProcess()
    {
        lock (stopProcessLock)
        {
            if (!processStarted) return;

            StopCheckToRestartProcessInterval().WaitResult();
            mqChannelPool.ResetGlobalChannel();
            processStarted = false;
            declareRabbitMqConfigurationFinished = false;
            waitingAckMessages.Clear();
        }
    }

    private bool DeclareRabbitMqConfiguration()
    {
        // retryCount: messageBusManager.AllDefinedBusMessageAndConsumerBindingRoutingKeys().Count
        // to update the dictionary for queue that need to force delete to re-declare queue
        return Util.TaskRunner.WaitRetryThrowFinalException(
            () =>
            {
                if (declareRabbitMqConfigurationFinished) return true;

                InitRabbitMqChannel();

                if (CurrentChannel != null)
                {
                    DeclareRabbitMqExchangesAndQueuesConfiguration();

                    declareRabbitMqConfigurationFinished = true;

                    return true;
                }

                return false;
            },
            retryAttempt => TimeSpan.Zero,
            retryCount: messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys().Count * 3); // Count * 3 to ensure retry declare queue works for all queues
    }

    private void StartConsumers()
    {
        try
        {
            // Config the prefectCount. "defines the max number of unacknowledged deliveries that are permitted on a channel" to limit messages to prevent rabbit mq down
            // Reference: https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html. Filter: BasicQos
            // QueuePrefetchCount : Default 1 to apply "Fair Dispatch"
            CurrentChannel.BasicQos(prefetchSize: 0, options.QueuePrefetchCount, false);

            // Config RabbitMQ Basic Consumer
            var applicationRabbitConsumer = new AsyncEventingBasicConsumer(CurrentChannel);
            applicationRabbitConsumer.Received += OnMessageReceived;

            // Binding all defined event bus consumer to RabbitMQ Basic Consumer
            messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys()
                .Select(GetConsumerQueueName)
                .ToList()
                .ForEach(
                    queueName =>
                    {
                        // autoAck: false -> the Consumer will ack manually.
                        CurrentChannel.BasicConsume(queueName, autoAck: false, applicationRabbitConsumer);

                        Logger.LogDebug(message: $"Consumer connected to queue {queueName}");
                    });
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
        await TransferMessageToAllMessageBusConsumers(rabbitMqMessage, currentCancellationToken);
    }

    private async Task TransferMessageToAllMessageBusConsumers(BasicDeliverEventArgs rabbitMqMessage, CancellationToken cancellationToken)
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
                .Select(
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
                    })
                .WhenAll();

            // WHY: After consumed message successfully, ack the message is handled to rabbitmq so that message could be removed.
            Util.TaskRunner.QueueActionInBackground(
                () =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    if (CurrentChannel?.IsClosed == false)
                        waitingAckMessages.TryAdd(GetWaitingAckMessageKey(rabbitMqMessage), null);

                    Util.TaskRunner.WaitRetryThrowFinalException(
                        () =>
                        {
                            // Check CurrentChannel != null because CurrentChannel could be null when process restart
                            // because some reason the rabbitmq server restart
                            if (waitingAckMessages.ContainsKey(GetWaitingAckMessageKey(rabbitMqMessage)) && CurrentChannel != null)
                                CurrentChannel.BasicAck(rabbitMqMessage.DeliveryTag, false);

                            waitingAckMessages.TryRemove(GetWaitingAckMessageKey(rabbitMqMessage), out _);
                        },
                        retryAttempt => TimeSpan.FromSeconds(AckMessageRetryDelaySeconds),
                        retryCount: AckMessageRetryCount,
                        ex => Logger.LogError(
                            ex,
                            "[MessageBus] Failed to ack the message. RoutingKey:{RoutingKey}. DeliveryTag:{DeliveryTag}",
                            rabbitMqMessage.RoutingKey,
                            rabbitMqMessage.DeliveryTag));
                },
                Logger,
                cancellationToken: cancellationToken);
        }
        catch (PlatformInvokeConsumerException ex)
        {
            Logger.LogError(
                ex,
                $"[MessageBus] Consume message error. [RoutingKey:{rabbitMqMessage.RoutingKey}].{Environment.NewLine}" +
                $"Message: {Encoding.UTF8.GetString(rabbitMqMessage.Body.Span)}");

            if (ProcessRequeueMessage(rabbitMqMessage, ex.BusMessage) == false)
            {
                if (CurrentChannel?.IsClosed == false)
                    waitingAckMessages.TryAdd(GetWaitingAckMessageKey(rabbitMqMessage), null);

                // Reject the message.
                Util.TaskRunner.QueueActionInBackground(
                    () =>
                        Util.TaskRunner.WaitRetryThrowFinalException(
                            () =>
                            {
                                // Check CurrentChannel != null because CurrentChannel could be null when process restart
                                // because some reason the rabbitmq server restart
                                if (waitingAckMessages.ContainsKey(GetWaitingAckMessageKey(rabbitMqMessage)) && CurrentChannel != null)
                                    CurrentChannel.BasicReject(rabbitMqMessage.DeliveryTag, false);

                                waitingAckMessages.TryRemove(GetWaitingAckMessageKey(rabbitMqMessage), out _);
                            },
                            retryAttempt => TimeSpan.FromSeconds(AckMessageRetryDelaySeconds),
                            retryCount: AckMessageRetryCount,
                            ex => Logger.LogError(
                                ex,
                                "[MessageBus] Failed to ack the message. RoutingKey:{RoutingKey}. DeliveryTag:{DeliveryTag}",
                                rabbitMqMessage.RoutingKey,
                                rabbitMqMessage.DeliveryTag)),
                    Logger,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                $"[MessageBus] Consume message error must REJECT. [RoutingKey:{rabbitMqMessage.RoutingKey}].{Environment.NewLine}" +
                $"Message: {Encoding.UTF8.GetString(rabbitMqMessage.Body.Span)}");

            // Reject the message.
            Util.TaskRunner.WaitRetry(
                () => CurrentChannel.BasicReject(rabbitMqMessage.DeliveryTag, false),
                retryAttempt => 10.Seconds(),
                retryCount: 10);
        }
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

    private static string GetWaitingAckMessageKey(BasicDeliverEventArgs rabbitMqMessage)
    {
        return $"{rabbitMqMessage.RoutingKey}_{rabbitMqMessage.DeliveryTag}";
    }

    private bool ProcessRequeueMessage(BasicDeliverEventArgs rabbitMqMessage, object busMessage)
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
                        CurrentChannel.BasicNack(rabbitMqMessage.DeliveryTag, multiple: true, requeue: true);

                        Logger.LogInformation(
                            message: $"RabbitMQ retry queue message for the routing key: {rabbitMqMessage.RoutingKey}.{Environment.NewLine}" +
                                     "Message: {BusMessage}",
                            busMessage.AsJson());
                    },
                    retryAttempt => TimeSpan.FromSeconds(options.ProcessRequeueMessageRetryDelaySeconds),
                    options.ProcessRequeueMessageRetryCount,
                    finalEx => Logger.LogError(
                        finalEx,
                        message: $"RabbitMQ retry queue failed message for the routing key: {rabbitMqMessage.RoutingKey}.{Environment.NewLine}" +
                                 "Message: {BusMessage}",
                        busMessage.AsJson()));

                return Task.CompletedTask;
            },
            PlatformApplicationGlobal.RootServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()),
            cancellationToken: currentCancellationToken);

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
            traceActivity?.SetTag("message", busMessage.AsJson());

            await PlatformMessageBusConsumer.InvokeConsumerAsync(
                consumer,
                busMessage,
                args.RoutingKey,
                options.IsLogConsumerProcessTime,
                options.LogErrorSlowProcessWarningTimeMilliseconds,
                Logger,
                currentCancellationToken);
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

    private IModel InitRabbitMqChannel()
    {
        try
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    options.InitRabbitMqChannelRetryCount,
                    retryAttempt => 10.Seconds())
                .ExecuteAndThrowFinalException(
                    () => mqChannelPool.InitGlobalChannel(),
                    ex =>
                    {
                        Logger.LogError(ex, "Init rabbit-mq channel failed.");
                    });
        }
        catch
        {
            return null;
        }
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
        var exchange = GetConsumerExchange(consumerBindingRoutingKey);
        var queueName = GetConsumerQueueName(consumerBindingRoutingKey);

        if (forceDeleteQueueBeforeDeclareQueues.Contains(queueName))
            CurrentChannel.QueueDelete(queueName, ifUnused: false, ifEmpty: false);

        // WHY: Set exclusive to false to support multiple consumers with the same type.
        // For example: in load balancing environment, we may have 2 instances of an API.
        // RabbitMQ will automatically apply load balancing behavior to send message to 1 instance only.
        // Catch exception without and ignore because the queue may existing with different arguments,
        // then create queue will be failed. It's ok to retry next time when QueueDelete success when queue has not messages
        Util.TaskRunner.CatchExceptionContinueThrow(
            () =>
            {
                QueueDeclare(CurrentChannel, queueName);
            },
            onException: ex =>
            {
                // If fail force delete queue and try create again. Accept that we could lost messages
                // Force delete queue to re-create queue with new arguments or options
                // At this time channel has been failed so need to re-init again by continue throw ex, it will retry with new added queue into forceDeleteQueueBeforeDeclareQueues
                forceDeleteQueueBeforeDeclareQueues.Add(queueName);
                mqChannelPool.ResetGlobalChannel();
            });

        DeclareQueueBindForConsumer(consumerBindingRoutingKey, queueName, exchange);

        void QueueDeclare(IModel model, string queueName)
        {
            // The "quorum" queue is a modern queue type for RabbitMQ implementing a durable, replicated FIFO queue based on the Raft consensus algorithm. https://www.rabbitmq.com/quorum-queues.html
            model.QueueDeclare(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: Util.DictionaryBuilder.New<string, object>(
                    ("x-expires", options.QueueUnusedExpireTime),
                    ("x-queue-type", "quorum"),
                    ("message-ttl", options.QueueMessagesTimeToLive),
                    ("x-max-in-memory-length", options.QueueMaxNumberMessagesInMemory)));
        }
    }

    private void DeclareQueueBindForConsumer(
        string consumerBindingRoutingKey,
        string queueName,
        string exchange)
    {
        CurrentChannel.QueueBind(queueName, exchange, consumerBindingRoutingKey);
        CurrentChannel.QueueBind(
            queueName,
            exchange,
            $"{consumerBindingRoutingKey}.{PlatformRabbitMqConstants.FanoutBindingChar}");

        Logger.LogDebug(
            message:
            $"Queue {queueName} has been declared and bound to Exchange {exchange} with routing key {consumerBindingRoutingKey} and {consumerBindingRoutingKey}.{PlatformRabbitMqConstants.FanoutBindingChar}");
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
                    CurrentChannel.ExchangeDeclare(exchangeName, ExchangeType.Topic, durable: true);
                });
    }

    private string GetConsumerExchange(PlatformBusMessageRoutingKey consumerRoutingKey)
    {
        return exchangeProvider.GetExchangeName(consumerRoutingKey);
    }
}
