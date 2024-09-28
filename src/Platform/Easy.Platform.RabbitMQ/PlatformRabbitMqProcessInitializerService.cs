#pragma warning disable IDE0055
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Easy.Platform.Application;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Timing;
using Easy.Platform.Infrastructures.MessageBus;
using Easy.Platform.Persistence;
using Easy.Platform.RabbitMQ.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// This service is responsible for initializing and managing the RabbitMQ message processing pipeline.
/// It configures RabbitMQ exchanges, declares queues for each consumer based on routing keys,
/// establishes connections to listen for incoming messages, and executes the appropriate consumers to handle those messages.
/// </summary>
public class PlatformRabbitMqProcessInitializerService : IDisposable
{
    /// <summary>
    /// The maximum number of times to retry acknowledging a message.
    /// </summary>
    public const int AckMessageRetryCount = int.MaxValue;

    /// <summary>
    /// The delay in seconds between each retry attempt for acknowledging a message.
    /// </summary>
    public const int AckMessageRetryDelaySeconds = 5;

    /// <summary>
    /// The <see cref="ActivitySource" /> used for tracing message processing operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(nameof(PlatformRabbitMqProcessInitializerService));

    /// <summary>
    /// The <see cref="TextMapPropagator" /> used for propagating tracing context across process boundaries.
    /// </summary>
    public static readonly TextMapPropagator TracingActivityPropagator = Propagators.DefaultTextMapPropagator;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly PlatformConsumerRabbitMqChannelPool channelPool;

    private readonly SemaphoreSlim connectConsumersToQueuesLock = new(1, 1);
    private readonly PlatformConsumeInboxBusMessageHostedService consumeInboxBusMessageHostedService;
    private readonly IPlatformRabbitMqExchangeProvider exchangeProvider;

    private readonly PlatformInboxBusMessageCleanerHostedService inboxBusMessageCleanerHostedService;
    private readonly PlatformMessageBusConfig messageBusConfig;
    private readonly IPlatformMessageBusScanner messageBusScanner;
    private readonly PlatformRabbitMqOptions options;
    private readonly PlatformOutboxBusMessageCleanerHostedService outboxBusMessageCleanerHostedService;
    private readonly SemaphoreSlim processMessageParallelLimitLock;

    private readonly SemaphoreSlim processRequeueMessageLock = new(1, 1);
    private readonly IPlatformRootServiceProvider rootServiceProvider;

    private readonly ConcurrentDictionary<string, List<KeyValuePair<Type, Type>>> routingKeyToCanProcessConsumerMessageTypeToConsumerTypesCacheMap = new();
    private readonly PlatformSendOutboxBusMessageHostedService sendOutboxBusMessageHostedService;

    private readonly SemaphoreSlim startProcessLock = new(initialCount: 1, maxCount: 1);
    private readonly SemaphoreSlim stopProcessLock = new(initialCount: 1, maxCount: 1);
    private readonly HashSet<IModel> usedChannels = [];
    private readonly ConcurrentDictionary<string, object> waitingAckMessages = new();
    private CancellationToken currentStartProcessCancellationToken;
    private bool disposed;
    private bool processStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformRabbitMqProcessInitializerService" /> class.
    /// </summary>
    /// <param name="applicationSettingContext">The application setting context.</param>
    /// <param name="exchangeProvider">The exchange provider.</param>
    /// <param name="messageBusScanner">The message bus scanner.</param>
    /// <param name="channelPool">The channel pool.</param>
    /// <param name="options">The RabbitMQ options.</param>
    /// <param name="messageBusConfig">The message bus configuration.</param>
    /// <param name="sendOutboxBusMessageHostedService">The send outbox bus message hosted service.</param>
    /// <param name="outboxBusMessageCleanerHostedService">The outbox bus message cleaner hosted service.</param>
    /// <param name="consumeInboxBusMessageHostedService">The consume inbox bus message hosted service.</param>
    /// <param name="inboxBusMessageCleanerHostedService">The inbox bus message cleaner hosted service.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="rootServiceProvider">The root service provider.</param>
    public PlatformRabbitMqProcessInitializerService(
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformRabbitMqExchangeProvider exchangeProvider,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformConsumerRabbitMqChannelPool channelPool,
        PlatformRabbitMqOptions options,
        PlatformMessageBusConfig messageBusConfig,
        PlatformSendOutboxBusMessageHostedService sendOutboxBusMessageHostedService,
        PlatformOutboxBusMessageCleanerHostedService outboxBusMessageCleanerHostedService,
        PlatformConsumeInboxBusMessageHostedService consumeInboxBusMessageHostedService,
        PlatformInboxBusMessageCleanerHostedService inboxBusMessageCleanerHostedService,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        this.applicationSettingContext = applicationSettingContext;
        this.exchangeProvider = exchangeProvider;
        this.messageBusScanner = messageBusScanner;
        this.channelPool = channelPool;
        this.options = options;
        this.messageBusConfig = messageBusConfig;
        this.sendOutboxBusMessageHostedService = sendOutboxBusMessageHostedService;
        this.outboxBusMessageCleanerHostedService = outboxBusMessageCleanerHostedService;
        this.consumeInboxBusMessageHostedService = consumeInboxBusMessageHostedService;
        this.inboxBusMessageCleanerHostedService = inboxBusMessageCleanerHostedService;
        this.rootServiceProvider = rootServiceProvider;
        Logger = loggerFactory.CreateLogger(GetType());

        processMessageParallelLimitLock = new SemaphoreSlim(this.options.MaxConcurrentProcessing, this.options.MaxConcurrentProcessing);
    }

    /// <summary>
    /// Gets the logger for this service.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets or sets a value indicating whether all required modules have been initialized to allow message consumption.
    /// </summary>
    protected bool CheckAllModulesInitiatedToAllowConsumeMessages { get; set; }

    /// <summary>
    /// Disposes of the resources used by this service.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously starts the RabbitMQ message processing pipeline.
    /// This involves declaring exchanges and queues, connecting consumers, and starting the hosted services responsible for sending, cleaning, and consuming messages.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task StartProcess(CancellationToken cancellationToken)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                await startProcessLock.WaitAsync(cancellationToken);

                currentStartProcessCancellationToken = cancellationToken;

                try
                {
                    if (processStarted) return;

                    Logger.LogInformation("[{TargetName}] RabbitMq init process STARTED", GetType().Name);

                    DeclareRabbitMqConfiguration();

                    await ConnectConsumersToQueues();

                    await Task.WhenAll(
                        sendOutboxBusMessageHostedService.StartAsync(currentStartProcessCancellationToken),
                        outboxBusMessageCleanerHostedService.StartAsync(currentStartProcessCancellationToken),
                        consumeInboxBusMessageHostedService.StartAsync(currentStartProcessCancellationToken),
                        inboxBusMessageCleanerHostedService.StartAsync(currentStartProcessCancellationToken));

                    processStarted = true;

                    Logger.LogInformation("[{TargetName}] RabbitMq init process FINISHED", GetType().Name);
                }
                finally
                {
                    startProcessLock.Release();
                }
            },
            _ => 10.Seconds(),
            int.MaxValue,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously stops the RabbitMQ message processing pipeline.
    /// This involves stopping the hosted services and clearing any pending acknowledgments.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
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
            stopProcessLock.Release();
        }
    }

    /// <summary>
    /// Declares the RabbitMQ exchanges and queues based on the configured consumers.
    /// This method initializes the RabbitMQ channel and retries the declaration process multiple times to handle transient issues.
    /// </summary>
    private void DeclareRabbitMqConfiguration()
    {
        InitRabbitMqChannel();

        // Retry multiple times to handle cases where the queue declaration might fail due to transient issues.
        Util.TaskRunner.WaitRetryThrowFinalException(
            DeclareRabbitMqExchangesAndQueuesConfiguration,
            retryAttempt => TimeSpan.Zero,
            retryCount: messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys().Count * 3); // Retry a few times for each defined queue.
    }

    /// <summary>
    /// Establishes connections for all defined consumers and starts listening for messages on their respective queues.
    /// This method split total channels equally to handle total queues. Example: ChannelPool max is 2. 10 queues => each channel handle 5 queues.
    /// </summary>
    private async Task ConnectConsumersToQueues()
    {
        try
        {
            await connectConsumersToQueuesLock.WaitAsync(currentStartProcessCancellationToken);

            if (usedChannels.Any())
            {
                usedChannels.ForEach(
                    p =>
                    {
                        if (p.IsOpen) p.Close();
                        p.Dispose();
                    });
                usedChannels.Clear();
            }

            Logger.LogInformation("Start connect all consumers to rabbitmq queue STARTED");

            var allQueueNames = messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys()
                .Select(GetConsumerQueueName)
                .ToList();
            var allParallelConsumerChannels = Enumerable.Range(0, options.ConsumerChannelPoolSize)
                .Select(p => channelPool.Get())
                .ToList();

            // Connect queue to channel
            var currentAssignChanelIndex = 0;
            allQueueNames.ForEach(
                async queueName =>
                {
                    var useChannel = allParallelConsumerChannels[currentAssignChanelIndex];

                    var applicationRabbitConsumer = new AsyncEventingBasicConsumer(useChannel)
                        .With(consumer => consumer.Received += OnMessageReceived);

                    // AutoAck is set to false to manually acknowledge messages after they have been successfully processed.
                    useChannel.BasicConsume(queueName, autoAck: false, applicationRabbitConsumer);

                    Logger.LogDebug("Consumers connected to queue {QueueName}", queueName);

                    if (currentAssignChanelIndex == allParallelConsumerChannels.Count - 1)
                        currentAssignChanelIndex = 0;
                    else
                        currentAssignChanelIndex += 1;

                    usedChannels.Add(useChannel);
                });

            // Return free channel back to pool
            allParallelConsumerChannels.ForEach(
                p =>
                {
                    if (!usedChannels.Contains(p)) channelPool.Return(p);
                });

            Logger.LogInformation("Start connect all consumers to rabbitmq queue FINISHED");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.BeautifyStackTrace(), "[{GetTypeFullName}] RabbitMq Consumer can't start", GetType().FullName);
            throw;
        }
        finally
        {
            connectConsumersToQueuesLock.Release();
        }
    }

    /// <summary>
    /// Generates the queue name for a consumer based on its routing key and the application name.
    /// </summary>
    /// <param name="consumerRoutingKey">The routing key of the consumer.</param>
    /// <returns>The generated queue name.</returns>
    private string GetConsumerQueueName(string consumerRoutingKey)
    {
        return $"[Platform][{applicationSettingContext.ApplicationName}]-{consumerRoutingKey}";
    }

    /// <summary>
    /// Event handler for when a message is received from RabbitMQ.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="rabbitMqMessage">The received RabbitMQ message.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs rabbitMqMessage)
    {
        await TransferMessageToAllMessageBusConsumers(sender.Cast<AsyncEventingBasicConsumer>().Model, rabbitMqMessage);
    }

    /// <summary>
    /// Transfers a received RabbitMQ message to all eligible message bus consumers.
    /// This method identifies the appropriate consumers for the message, deserializes the message,
    /// and enqueues it for processing.
    /// </summary>
    private async Task TransferMessageToAllMessageBusConsumers(IModel channel, BasicDeliverEventArgs rabbitMqMessage)
    {
        try
        {
            // Ensure all required modules are initialized before processing messages.
            if (!CheckAllModulesInitiatedToAllowConsumeMessages)
                await IPlatformModule.WaitAllModulesInitiatedAsync(
                    rootServiceProvider,
                    typeof(IPlatformPersistenceModule),
                    Logger,
                    "to start connect all consumers to rabbitmq queue");
            CheckAllModulesInitiatedToAllowConsumeMessages = true;

            // Identify all consumers that can handle the message based on the routing key.
            var canProcessConsumerTypeToBusMessagePairs = rabbitMqMessage.RoutingKey.Pipe(
                    routingKey =>
                    {
                        var consumerMessageTypeToConsumerTypePairs = routingKeyToCanProcessConsumerMessageTypeToConsumerTypesCacheMap.GetOrAdd(
                            routingKey,
                            key => GetCanProcessConsumerTypes(key)
                                .Pipe(
                                    consumerTypes => consumerTypes.SelectList(
                                        consumerType =>
                                        {
                                            // Determine the message type expected by the consumer.
                                            var consumerMessageType = PlatformMessageBusConsumer.GetConsumerMessageType(consumerType);

                                            return new KeyValuePair<Type, Type>(consumerMessageType, consumerType);
                                        }))
                        );

                        return consumerMessageTypeToConsumerTypePairs;
                    })
                .GroupBy(consumerMessageTypeToConsumerTypePair => consumerMessageTypeToConsumerTypePair.Key, p => p.Value)
                .SelectMany(
                    consumerMessageTypeToConsumerTypesGroup =>
                    {
                        var consumerMessageType = consumerMessageTypeToConsumerTypesGroup.Key;

                        // Deserialize the message body into the appropriate message type.
                        var busMessage = Util.TaskRunner.CatchExceptionFallBackValue<Exception, object>(
                            () => PlatformJsonSerializer.Deserialize(
                                rabbitMqMessage.Body.Span,
                                consumerMessageType),
                            ex => Logger.LogError(
                                ex.BeautifyStackTrace(),
                                "RabbitMQ parsing message to {ConsumerMessageType.Name} error for the routing key {Args.RoutingKey}. Body: {Args.Body}",
                                consumerMessageType.Name,
                                rabbitMqMessage.RoutingKey,
                                Encoding.UTF8.GetString(rabbitMqMessage.Body.Span)),
                            null);

                        return consumerMessageTypeToConsumerTypesGroup.Select(consumerType => new KeyValuePair<Type, object>(consumerType, busMessage));
                    })
                .ToList();

            Util.TaskRunner.QueueActionInBackground(
                () => ExecuteCanProcessMessageBusConsumers(
                    canProcessConsumerTypeToBusMessagePairs: canProcessConsumerTypeToBusMessagePairs,
                    channel: channel,
                    rabbitMqMessageArgs: rabbitMqMessage,
                    currentStartProcessCancellationToken),
                loggerFactory: () => Logger,
                cancellationToken: currentStartProcessCancellationToken,
                logFullStackTraceBeforeBackgroundTask: false);
        }
        catch (Exception ex)
        {
            // If an error occurs during message processing, log the error and reject the message.
            Logger.LogError(
                ex.BeautifyStackTrace(),
                "[MessageBus] Consume message error must REJECT. [RoutingKey:{RoutingKey}]. Message: {Message}",
                rabbitMqMessage.RoutingKey,
                Encoding.UTF8.GetString(rabbitMqMessage.Body.Span));

            AckMessage(channel, rabbitMqMessage, isReject: true);
        }
    }

    /// <summary>
    /// Executes a list of consumers that can process a given message.
    /// This method runs each eligible consumer in parallel and handles acknowledgment or rejection of the message.
    /// </summary>
    /// <param name="canProcessConsumerTypeToBusMessagePairs">A list of consumer types and their corresponding deserialized messages.</param>
    /// <param name="channel">The RabbitMQ channel the message was received on.</param>
    /// <param name="rabbitMqMessageArgs">The arguments associated with the received RabbitMQ message.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    private async Task ExecuteCanProcessMessageBusConsumers(
        List<KeyValuePair<Type, object>> canProcessConsumerTypeToBusMessagePairs,
        IModel channel,
        BasicDeliverEventArgs rabbitMqMessageArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            await processMessageParallelLimitLock.WaitAsync(currentStartProcessCancellationToken);

            // Execute each consumer in parallel.
            await canProcessConsumerTypeToBusMessagePairs
                .ParallelAsync(
                    async consumerTypeBusMessagePair =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        // Extract tracing context from the message properties.
                        var parentContext = TracingActivityPropagator.Extract(
                            default,
                            rabbitMqMessageArgs.BasicProperties,
                            ExtractTraceContextFromBasicProperties);

                        // Create a new activity for tracing this consumer execution.
                        using (var activity = ActivitySource.StartActivity(
                            $"MessageBus.{nameof(ExecuteConsumer)}",
                            ActivityKind.Consumer,
                            parentContext.ActivityContext))
                        {
                            using (var scope = rootServiceProvider.CreateScope())
                            {
                                // Resolve the consumer instance from the service provider.
                                var consumer = scope.ServiceProvider.GetService(consumerTypeBusMessagePair.Key).Cast<IPlatformMessageBusConsumer>();

                                if (consumer != null)
                                    await ExecuteConsumer(rabbitMqMessageArgs, consumerTypeBusMessagePair.Value, consumer, activity);
                            }
                        }
                    });

            // Acknowledge the message after successful processing by all consumers.
            AckMessage(channel, rabbitMqMessageArgs, isReject: false);
        }
        catch (PlatformInvokeConsumerException ex)
        {
            // If an error occurs during consumer invocation, log the error and attempt to requeue the message.
            Logger.LogError(
                ex.BeautifyStackTrace(),
                "[MessageBus] Consume message error. [RoutingKey:{RoutingKey}]. Message: {Message}",
                rabbitMqMessageArgs.RoutingKey,
                Encoding.UTF8.GetString(rabbitMqMessageArgs.Body.Span));

            // If requeueing fails, reject the message.
            if (ProcessRequeueMessage(channel, rabbitMqMessageArgs, ex.BusMessage) == false)
                AckMessage(channel, rabbitMqMessageArgs, isReject: true);
        }
        finally
        {
            processMessageParallelLimitLock.Release();
        }
    }

    /// <summary>
    /// Acknowledges or rejects a message on the RabbitMQ channel.
    /// This method handles the acknowledgment process in the background to avoid blocking the message processing loop.
    /// </summary>
    public void AckMessage(IModel channel, BasicDeliverEventArgs rabbitMqMessageArgs, bool isReject)
    {
        // Acknowledge the message in the background to avoid blocking the message processing loop.
        Util.TaskRunner.QueueActionInBackground(
            () =>
            {
                var waitingAckMessageKey = GetWaitingAckMessageKey(rabbitMqMessageArgs, channel);

                waitingAckMessages.TryAdd(waitingAckMessageKey, null);

                // Retry acknowledging the message multiple times in case of transient errors.
                Util.TaskRunner.WaitRetryThrowFinalException(
                    async () =>
                    {
                        if (!channel.IsClosedPermanently())
                        {
                            if (channel.IsClosed) throw new Exception("Channel is temporarily closed. Try again later");

                            if (waitingAckMessages.ContainsKey(waitingAckMessageKey))
                            {
                                if (isReject)
                                    channel.BasicReject(rabbitMqMessageArgs.DeliveryTag, false);
                                else
                                    channel.BasicAck(rabbitMqMessageArgs.DeliveryTag, false);
                            }

                            waitingAckMessages.TryRemove(waitingAckMessageKey, out _);
                        }
                        else
                        {
                            // If something go wrong, chanel is invalid, then reconnect all channel again
                            await ConnectConsumersToQueues();
                        }
                    },
                    retryAttempt => AckMessageRetryDelaySeconds.Seconds(),
                    retryCount: AckMessageRetryCount,
                    ex => Logger.LogError(
                        ex.BeautifyStackTrace(),
                        "[MessageBus] Failed to ack the message. RoutingKey:{RoutingKey}. DeliveryTag:{DeliveryTag}",
                        rabbitMqMessageArgs.RoutingKey,
                        rabbitMqMessageArgs.DeliveryTag),
                    onRetry: (ex, delayTime, retryAttempt, arg4) =>
                    {
                        if (retryAttempt >= 10)
                            Logger.LogError(
                                ex.BeautifyStackTrace(),
                                "[MessageBus] Retry to ack the message failed. RoutingKey:{RoutingKey}. DeliveryTag:{DeliveryTag}",
                                rabbitMqMessageArgs.RoutingKey,
                                rabbitMqMessageArgs.DeliveryTag);
                    });
            },
            () => Logger,
            cancellationToken: CancellationToken.None,
            logFullStackTraceBeforeBackgroundTask: false);
    }

    /// <summary>
    /// Retrieves a list of consumer types that are capable of processing messages with the specified routing key.
    /// This method checks both explicit routing key attributes and default routing key conventions.
    /// </summary>
    private List<Type> GetCanProcessConsumerTypes(string messageRoutingKey)
    {
        return messageBusScanner
            .ScanAllDefinedConsumerTypes()
            .Where(
                messageBusConsumerType =>
                {
                    // If the consumer doesn't have explicit routing key attributes, check if it matches the default routing key convention.
                    if (messageBusConsumerType.GetCustomAttributes<PlatformConsumerRoutingKeyAttribute>().IsEmpty())
                    {
                        var consumerGenericType = messageBusConsumerType.FindMatchedGenericType(typeof(IPlatformMessageBusConsumer<>));

                        var matchedDefaultMessageRoutingKey =
                            IPlatformMessageBusConsumer.BuildForConsumerDefaultBindingRoutingKey(consumerGenericType);

                        return matchedDefaultMessageRoutingKey.Match(messageRoutingKey);
                    }

                    // If the consumer has explicit routing key attributes, check if any of them match the message routing key.
                    return PlatformConsumerRoutingKeyAttribute.CanMessageBusConsumerProcess(
                        messageBusConsumerType,
                        messageRoutingKey);
                })
            .Select(
                consumerType => new
                {
                    ConsumerType = consumerType,
                    // Determine the execution order of the consumer.
                    ConsumerExecuteOrder = rootServiceProvider.ExecuteScoped(
                        scope => scope.ServiceProvider.GetService(consumerType).Cast<IPlatformMessageBusConsumer>().ExecuteOrder())
                })
            .OrderBy(p => p.ConsumerExecuteOrder)
            .Select(p => p.ConsumerType)
            .ToList();
    }

    /// <summary>
    /// Generates a unique key for tracking messages waiting to be acknowledged.
    /// This key is used to ensure that each message is acknowledged only once.
    /// </summary>
    /// <param name="rabbitMqMessage">The received RabbitMQ message.</param>
    /// <param name="channel">The RabbitMQ channel the message was received on.</param>
    /// <returns>A unique key for the message.</returns>
    private static string GetWaitingAckMessageKey(BasicDeliverEventArgs rabbitMqMessage, IModel channel)
    {
        return $"{rabbitMqMessage.RoutingKey}_Channel:{channel.ChannelNumber}_{rabbitMqMessage.DeliveryTag}";
    }

    /// <summary>
    /// Attempts to requeue a message for later processing.
    /// This method checks if the message has exceeded its requeue expiration time and handles the requeue process.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel the message was received on.</param>
    /// <param name="rabbitMqMessage">The received RabbitMQ message.</param>
    /// <param name="busMessage">The deserialized message object.</param>
    /// <returns>True if the message was successfully requeued; otherwise, false.</returns>
    private bool ProcessRequeueMessage(IModel channel, BasicDeliverEventArgs rabbitMqMessage, object busMessage)
    {
        var messageCreatedDate = busMessage.As<IPlatformTrackableBusMessage>()?.CreatedUtcDate;
        // Check if the message has exceeded its requeue expiration time.
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
            async () =>
            {
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () =>
                    {
                        try
                        {
                            await processRequeueMessageLock.WaitAsync(currentStartProcessCancellationToken);

                            if (!channel.IsClosedPermanently())
                            {
                                if (channel.IsClosed) throw new Exception("Channel is temporarily closed. Try again later");

                                channel.BasicNack(rabbitMqMessage.DeliveryTag, multiple: true, requeue: true);

                                Logger.LogWarning(
                                    message: "RabbitMQ retry queue message for the routing key: {RoutingKey}. " +
                                             "Message: {BusMessage}",
                                    rabbitMqMessage.RoutingKey,
                                    busMessage.ToJson());
                            }
                            else
                            {
                                // If something go wrong, chanel is invalid, then reconnect all channel again
                                await ConnectConsumersToQueues();
                            }
                        }
                        finally
                        {
                            processRequeueMessageLock.Release();
                        }
                    },
                    retryAttempt => options.ProcessRequeueMessageRetryDelaySeconds.Seconds(),
                    retryCount: options.ProcessRequeueMessageRetryCount,
                    finalEx => Logger.LogError(
                        finalEx.BeautifyStackTrace(),
                        message: "RabbitMQ retry queue failed message for the routing key: {RoutingKey}. " +
                                 "Message: {BusMessage}",
                        rabbitMqMessage.RoutingKey,
                        busMessage.ToJson()),
                    onRetry: (ex, delayTime, retryAttempt, arg4) =>
                    {
                        Logger.LogError(
                            ex.BeautifyStackTrace(),
                            "[MessageBus] Retry to Requeue the message failed. RoutingKey:{RoutingKey}. DeliveryTag:{DeliveryTag}",
                            rabbitMqMessage.RoutingKey,
                            rabbitMqMessage.DeliveryTag);
                    },
                    cancellationToken: currentStartProcessCancellationToken);
            },
            loggerFactory: () => Logger,
            cancellationToken: currentStartProcessCancellationToken,
            logFullStackTraceBeforeBackgroundTask: false);

        return true;
    }

    /// <summary>
    /// Executes a consumer for a given message.
    /// This method invokes the consumer and handles tracing for the consumer execution.
    /// </summary>
    private async Task ExecuteConsumer(
        BasicDeliverEventArgs rabbitMqMessageArgs,
        object deserializedBusMessage,
        IPlatformMessageBusConsumer consumer,
        Activity traceActivity = null)
    {
        if (deserializedBusMessage != null)
        {
            traceActivity?.SetTag("consumer", consumer.GetType().Name);
            traceActivity?.SetTag("message", deserializedBusMessage.ToFormattedJson());

            await PlatformMessageBusConsumer.InvokeConsumerAsync(
                consumer,
                deserializedBusMessage,
                rabbitMqMessageArgs.RoutingKey,
                messageBusConfig,
                Logger);
        }
    }

    /// <summary>
    /// Extracts tracing context from the basic properties of a RabbitMQ message.
    /// This method is used to propagate tracing information across process boundaries.
    /// </summary>
    private List<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers != null && props.Headers.ContainsKey(key))
            {
                props.Headers.TryGetValue(key, out var value);

                return [Encoding.UTF8.GetString(value.As<byte[]>() ?? [])];
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.BeautifyStackTrace(), "Failed to extract trace context");
        }

        return [];
    }

    /// <summary>
    /// Initializes the RabbitMQ channel.
    /// This method attempts to initialize the first channel in the channel pool, retrying if necessary.
    /// </summary>
    private void InitRabbitMqChannel()
    {
        Util.TaskRunner.WaitRetryThrowFinalException(
            () => channelPool.TryInitFirstChannel(),
            retryCount: options.InitRabbitMqChannelRetryCount,
            onRetry: (exception, delayTime, retryAttempt, context) =>
            {
                if (retryAttempt >= options.MinimumRetryTimesToLogError)
                    Logger.LogError("Retry Init rabbit-mq channel. {Error}", exception.Message);
            },
            onBeforeThrowFinalExceptionFn: ex =>
            {
                Logger.LogError(ex.BeautifyStackTrace(), "Init rabbit-mq channel failed.");
            });
    }

    /// <summary>
    /// Declares RabbitMQ exchanges and queues configuration.
    /// This method scans all defined consumer routing keys and declares the necessary exchanges and queues.
    /// </summary>
    private void DeclareRabbitMqExchangesAndQueuesConfiguration()
    {
        // Get exchange routing key for all consumers in source code
        var allDefinedMessageBusConsumerPatternRoutingKeys = messageBusScanner.ScanAllDefinedConsumerBindingRoutingKeys();

        // Declare all exchanges
        DeclareExchangesForRoutingKeys(allDefinedMessageBusConsumerPatternRoutingKeys);
        // Declare all queues
        allDefinedMessageBusConsumerPatternRoutingKeys.ForEach(consumerRoutingKey => DeclareQueueForConsumer(consumerRoutingKey));
    }

    /// <summary>
    /// Declares a queue for a specific consumer.
    /// This method handles the queue declaration process, including error handling and queue binding.
    /// </summary>
    private void DeclareQueueForConsumer(PlatformBusMessageRoutingKey consumerBindingRoutingKey)
    {
        channelPool.GetChannelDoActionAndReturn(
            currentChannel =>
            {
                var exchange = GetConsumerExchange(consumerBindingRoutingKey);
                var queueName = GetConsumerQueueName(consumerBindingRoutingKey);

                try
                {
                    DeclareQueueAndBindForConsumer(currentChannel, queueName, exchange);
                }
                catch (Exception)
                {
                    // If failed because queue is existing with different configurations/args, try to delete and declare again with new configuration
                    if (currentChannel.CloseReason?.ReplyCode == RabbitMqCloseReasonCodes.NotAcceptable)
                    {
                        try
                        {
                            channelPool.GetChannelDoActionAndReturn(p => QueueDelete(p, queueName));
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(
                                "RabbitMQ failed try to delete queue to declare new queue with updated configuration: {Error}. If the queue still have messages, please process it or manually delete the queue. We still are using the old queue configuration",
                                e.Message);

                            // If delete queue failed, just ACCEPT using the current old one is OK
                            channelPool.GetChannelDoActionAndReturn(channel => DeclareQueueBindForConsumer(channel, consumerBindingRoutingKey, queueName, exchange));

                            // Then schedule in background when ever queue is empty then delete and re-declare queue right away
                            Util.TaskRunner.QueueActionInBackground(
                                async () =>
                                {
                                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                                        async () =>
                                        {
                                            channelPool.GetChannelDoActionAndReturn(channel => QueueDelete(channel, queueName));

                                            channelPool.GetChannelDoActionAndReturn(channel => DeclareQueueAndBindForConsumer(channel, queueName, exchange));
                                        },
                                        cancellationToken: currentStartProcessCancellationToken,
                                        sleepDurationProvider: retryAttempt => 10.Seconds(),
                                        retryCount: int.MaxValue);
                                },
                                loggerFactory: () => Logger,
                                cancellationToken: CancellationToken.None,
                                logFullStackTraceBeforeBackgroundTask: false);

                            return;
                        }

                        channelPool.GetChannelDoActionAndReturn(channel => DeclareQueueAndBindForConsumer(channel, queueName, exchange));
                    }
                    else
                    {
                        throw;
                    }
                }
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
                    //("x-queue-type", "quorum"), : Use basic queue with publish message persistent properties is true, still support node restart, trade off availability for better latency
                    ("message-ttl", options.QueueMessagesTimeToLive)
                    /*("x-max-in-memory-length", options.QueueMaxNumberMessagesInMemory)*/));
        }

        void DeclareQueueAndBindForConsumer(IModel channel, string queueName, string exchange)
        {
            DeclareQueue(channel, queueName);

            DeclareQueueBindForConsumer(channel, consumerBindingRoutingKey, queueName, exchange);
        }
    }

    private static void QueueDelete(IModel channel, string queueName)
    {
        // Check if the queue exists and get its message count and consumer count
        var queueInfo = channel.QueueDeclarePassive(queueName);

        // Check if the queue is empty message count is 0
        if (queueInfo.MessageCount == 0)
            // Queue is empty, delete it unconditionally (without if-empty flag)
            channel.QueueDelete(queueName);
        else throw new Exception("Queue must be empty and no ConsumerCount use it");
    }

    /// <summary>
    /// Declares queue bindings for a consumer.
    /// This method binds the queue to the exchange with the appropriate routing keys.
    /// </summary>
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

        Logger.LogDebug(
            message:
            "Queue {QueueName} has been declared. Exchange:{Exchange}. RoutingKey:{ConsumerBindingRoutingKey}",
            queueName,
            exchange,
            consumerBindingRoutingKey);
    }

    /// <summary>
    /// Declares exchanges for a list of routing keys.
    /// This method groups routing keys by exchange and declares each unique exchange.
    /// </summary>
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
        if (!disposed)
        {
            if (disposing)
            {
                // Release managed resources
                startProcessLock?.Dispose();
                stopProcessLock?.Dispose();
                processMessageParallelLimitLock.Dispose();
                processRequeueMessageLock.Dispose();
                connectConsumersToQueuesLock.Dispose();
            }

            // Release unmanaged resources

            disposed = true;
        }
    }
}
