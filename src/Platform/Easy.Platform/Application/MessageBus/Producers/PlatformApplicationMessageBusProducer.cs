using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers;

/// <summary>
/// Provides methods for producing and sending messages to the message bus in platform applications.
/// This interface defines methods for sending messages with various options, such as tracking IDs, message groups,
/// message actions, and support for the Outbox Pattern.
/// </summary>
public interface IPlatformApplicationBusMessageProducer
{
    /// <summary>
    /// Sends a message to the bus with a specific payload and routing key.
    /// The routing key is constructed based on the message type, application name, and optional message action.
    /// If <paramref name="forceUseDefaultRoutingKey" /> is true or the message does not implement <see cref="IPlatformSelfRoutingKeyBusMessage" />,
    /// the default routing key is used (<see cref="PlatformBusMessageRoutingKey.BuildDefaultRoutingKey" />).
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="TMessagePayload">The type of the message payload.</typeparam>
    /// <param name="trackId">A unique identifier for tracking the message.</param>
    /// <param name="messagePayload">The payload of the message.</param>
    /// <param name="messageGroup">The group of the message.</param>
    /// <param name="messageAction">An optional action associated with the message.</param>
    /// <param name="autoSaveOutboxMessage">Whether to automatically save the message to the outbox if supported.</param>
    /// <param name="forceUseDefaultRoutingKey">Whether to force the use of the default routing key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent message.</returns>
    Task<TMessage> SendAsync<TMessage, TMessagePayload>(
        string trackId,
        TMessagePayload messagePayload,
        string messageGroup = null,
        string messageAction = null,
        bool autoSaveOutboxMessage = true,
        bool forceUseDefaultRoutingKey = false,
        CancellationToken cancellationToken = default)
        where TMessage : class, IPlatformWithPayloadBusMessage<TMessagePayload>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
        where TMessagePayload : class, new();

    /// <summary>
    /// Sends a message to the bus.
    /// If <paramref name="forceUseDefaultRoutingKey" /> is true or the message does not implement <see cref="IPlatformSelfRoutingKeyBusMessage" />,
    /// the default routing key is used (<see cref="PlatformBusMessageRoutingKey.BuildDefaultRoutingKey" />).
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="autoSaveOutboxMessage">Whether to automatically save the message to the outbox if supported.</param>
    /// <param name="forceUseDefaultRoutingKey">Whether to force the use of the default routing key.</param>
    /// <param name="sourceOutboxUowId">The ID of the unit of work that originated the outbox message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent message.</returns>
    Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        bool autoSaveOutboxMessage = true,
        bool forceUseDefaultRoutingKey = false,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default)
        where TMessage : class, new();

    /// <summary>
    /// Checks if outbox message support is available.
    /// </summary>
    /// <returns>True if outbox message support is available; otherwise, false.</returns>
    public bool HasOutboxMessageSupport();
}

/// <summary>
/// A concrete implementation of <see cref="IPlatformApplicationBusMessageProducer" /> for producing and sending messages in platform applications.
/// This class provides methods for building messages, sending them to the bus, and managing outbox messages.
/// </summary>
public class PlatformApplicationBusMessageProducer : IPlatformApplicationBusMessageProducer
{
    private readonly Lazy<ILogger<PlatformApplicationBusMessageProducer>> loggerLazy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformApplicationBusMessageProducer" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="applicationSettingContext">The application setting context.</param>
    /// <param name="requestContextAccessor">The request context accessor.</param>
    /// <param name="outboxConfig">The configuration for the outbox pattern.</param>
    /// <param name="unitOfWorkManager">The unit of work manager.</param>
    public PlatformApplicationBusMessageProducer(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        PlatformOutboxConfig outboxConfig,
        IPlatformUnitOfWorkManager unitOfWorkManager)
    {
        ServiceProvider = serviceProvider;
        loggerLazy = new Lazy<ILogger<PlatformApplicationBusMessageProducer>>(() => loggerFactory.CreateLogger<PlatformApplicationBusMessageProducer>());
        MessageBusProducer = serviceProvider.GetService<IPlatformMessageBusProducer>() ?? new PlatformPseudoMessageBusProducer();
        ApplicationSettingContext = applicationSettingContext;
        RequestContextAccessor = requestContextAccessor;
        OutboxConfig = outboxConfig;
        UnitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// Gets the service provider for the current scope.
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the logger for this class.
    /// </summary>
    protected ILogger<PlatformApplicationBusMessageProducer> Logger => loggerLazy.Value;

    /// <summary>
    /// Gets the message bus producer.
    /// </summary>
    protected IPlatformMessageBusProducer MessageBusProducer { get; }

    /// <summary>
    /// Gets the application setting context.
    /// </summary>
    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    /// <summary>
    /// Gets the request context accessor.
    /// </summary>
    protected IPlatformApplicationRequestContextAccessor RequestContextAccessor { get; }

    /// <summary>
    /// Gets the configuration for the outbox pattern.
    /// </summary>
    protected PlatformOutboxConfig OutboxConfig { get; }

    /// <summary>
    /// Gets the unit of work manager.
    /// </summary>
    protected IPlatformUnitOfWorkManager UnitOfWorkManager { get; }

    /// <inheritdoc />
    public async Task<TMessage> SendAsync<TMessage, TMessagePayload>(
        string trackId,
        TMessagePayload messagePayload,
        string messageGroup = null,
        string messageAction = null,
        bool autoSaveOutboxMessage = true,
        bool forceUseDefaultRoutingKey = false,
        CancellationToken cancellationToken = default)
        where TMessage : class, IPlatformWithPayloadBusMessage<TMessagePayload>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
        where TMessagePayload : class, new()
    {
        // Build the message with the provided payload and routing key information.
        var message = BuildPlatformBusMessage<TMessage, TMessagePayload>(trackId, messagePayload, messageGroup, messageAction);

        // Send the message to the bus, using the appropriate routing key and outbox options.
        return await SendMessageAsync(
            message,
            forceUseDefaultRoutingKey
                ? PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(message.GetType(), ApplicationSettingContext.ApplicationName)
                : message.RoutingKey(),
            autoSaveOutboxMessage,
            UnitOfWorkManager.TryGetCurrentActiveUow()?.Id,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        bool autoSaveOutboxMessage = true,
        bool forceUseDefaultRoutingKey = false,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        // Send the message to the bus, using the appropriate routing key and outbox options.
        return await SendMessageAsync(
            message,
            routingKey: forceUseDefaultRoutingKey || message.As<IPlatformSelfRoutingKeyBusMessage>() == null
                ? PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(message.GetType(), ApplicationSettingContext.ApplicationName)
                : message.As<IPlatformSelfRoutingKeyBusMessage>().RoutingKey(),
            autoSaveOutboxMessage,
            sourceOutboxUowId ?? UnitOfWorkManager.TryGetCurrentActiveUow()?.Id,
            cancellationToken);
    }

    /// <inheritdoc />
    public bool HasOutboxMessageSupport()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    /// <summary>
    /// Builds a platform event bus message identity object, containing information about the user and request context.
    /// </summary>
    /// <returns>A <see cref="PlatformBusMessageIdentity" /> object.</returns>
    protected PlatformBusMessageIdentity BuildPlatformEventBusMessageIdentity()
    {
        return new PlatformBusMessageIdentity
        {
            UserId = RequestContextAccessor.Current.UserId(),
            RequestId = RequestContextAccessor.Current.RequestId(),
            UserName = RequestContextAccessor.Current.UserName()
        };
    }

    /// <summary>
    /// Sends a message to the bus, handling outbox message persistence if enabled.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="autoSaveOutboxMessage">Whether to automatically save the message to the outbox if supported.</param>
    /// <param name="sourceOutboxUowId">The ID of the unit of work that originated the outbox message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent message.</returns>
    protected virtual async Task<TMessage> SendMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        bool autoSaveOutboxMessage,
        string sourceOutboxUowId,
        CancellationToken cancellationToken)
        where TMessage : class, new()
    {
        // If the message is trackable, set its tracking information.
        if (message is IPlatformTrackableBusMessage trackableBusMessage)
        {
            trackableBusMessage.TrackingId ??= Ulid.NewUlid().ToString();
            trackableBusMessage.ProduceFrom ??= ApplicationSettingContext.ApplicationName;
            trackableBusMessage.CreatedUtcDate ??= DateTime.UtcNow;
            if (trackableBusMessage.RequestContext == null || trackableBusMessage.RequestContext.IsEmpty())
                trackableBusMessage.RequestContext = RequestContextAccessor.Current.GetAllKeyValues(ApplicationSettingContext.GetIgnoreRequestContextKeys());
        }

        // If outbox message support is enabled and auto-saving is requested, save the message to the outbox.
        if (autoSaveOutboxMessage && HasOutboxMessageSupport())
        {
            var outboxEventBusProducerHelper = ServiceProvider.GetRequiredService<PlatformOutboxMessageBusProducerHelper>();

            await outboxEventBusProducerHelper.HandleSendingOutboxMessageAsync(
                message,
                routingKey,
                OutboxConfig.RetryProcessFailedMessageInSecondsUnit,
                subQueueMessageIdPrefix: message.As<IPlatformSubMessageQueuePrefixSupport>()?.SubQueuePrefix(),
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage: true,
                handleExistingOutboxMessage: null,
                sourceOutboxUowId: sourceOutboxUowId,
                cancellationToken);

            return message;
        }

        // If outbox message support is not enabled or auto-saving is not requested, send the message directly to the bus.
        return await MessageBusProducer.SendAsync(message, routingKey, cancellationToken);
    }

    /// <summary>
    /// Builds a platform bus message with a specific payload and routing key information.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="TMessagePayload">The type of the message payload.</typeparam>
    /// <param name="trackId">A unique identifier for tracking the message.</param>
    /// <param name="messagePayload">The payload of the message.</param>
    /// <param name="messageGroup">The group of the message.</param>
    /// <param name="messageAction">An optional action associated with the message.</param>
    /// <returns>The built message.</returns>
    protected TMessage BuildPlatformBusMessage<TMessage, TMessagePayload>(string trackId, TMessagePayload messagePayload, string messageGroup, string messageAction)
        where TMessage : class, IPlatformWithPayloadBusMessage<TMessagePayload>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
        where TMessagePayload : class, new()
    {
        return PlatformBusMessage<TMessagePayload>.New<TMessage>(
            trackId,
            payload: messagePayload,
            identity: BuildPlatformEventBusMessageIdentity(),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: messageGroup,
            messageAction: messageAction,
            requestContext: RequestContextAccessor.Current.GetAllKeyValues(ApplicationSettingContext.GetIgnoreRequestContextKeys()));
    }

    /// <summary>
    /// A pseudo message bus producer that does not actually send messages to the bus.
    /// This is used when no real message bus producer is configured.
    /// </summary>
    public class PlatformPseudoMessageBusProducer : IPlatformMessageBusProducer
    {
        /// <inheritdoc />
        public Task<TMessage> SendAsync<TMessage>(
            TMessage message,
            string routingKey,
            CancellationToken cancellationToken = default) where TMessage : class, new()
        {
            return Task.FromResult(message);
        }
    }
}
