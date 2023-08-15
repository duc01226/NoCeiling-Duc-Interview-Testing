using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers;

public interface IPlatformApplicationBusMessageProducer
{
    /// <summary>
    /// Send message to bus. Routing key will be "{TMessage.MessageGroup}.{ApplicationSettingContext.ApplicationName}.{TMessage.MessageType}.{<see cref="messageAction" />}" <br />
    /// If forceUseDefaultRoutingKey = true or message is not <see cref="IPlatformSelfRoutingKeyBusMessage" /> then Use Default RoutingKey <see cref="PlatformBusMessageRoutingKey.BuildDefaultRoutingKey" />
    /// </summary>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TMessagePayload">Message payload type</typeparam>
    /// <param name="trackId">A random unique string to be used to track the message history, where is it from or for logging</param>
    /// <param name="messagePayload">Message payload</param>
    /// <param name="messageAction">Optional message action to be used as routing key for consumer filtering</param>
    /// <param name="autoSaveOutboxMessage">If true, auto save message as outbox message if outbox message is supported</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>Return sent Message</returns>
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
    /// Send message to bus. If forceUseDefaultRoutingKey = true or message is not
    /// <see cref="IPlatformSelfRoutingKeyBusMessage" /> then Use Default RoutingKey
    /// <see cref="PlatformBusMessageRoutingKey.BuildDefaultRoutingKey" />
    /// </summary>
    Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        bool autoSaveOutboxMessage = true,
        bool forceUseDefaultRoutingKey = false,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default)
        where TMessage : class, new();

    public bool HasOutboxMessageSupport();
}

public class PlatformApplicationBusMessageProducer : IPlatformApplicationBusMessageProducer
{
    public PlatformApplicationBusMessageProducer(
        IServiceProvider serviceProvider,
        ILogger<PlatformApplicationBusMessageProducer> logger,
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        PlatformOutboxConfig outboxConfig)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
        MessageBusProducer = serviceProvider.GetService<IPlatformMessageBusProducer>() ?? new PlatformPseudoMessageBusProducer();
        ApplicationSettingContext = applicationSettingContext;
        UserContextAccessor = userContextAccessor;
        OutboxConfig = outboxConfig;
    }

    protected IServiceProvider ServiceProvider { get; }
    protected ILogger<PlatformApplicationBusMessageProducer> Logger { get; }
    protected IPlatformMessageBusProducer MessageBusProducer { get; }
    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }
    protected IPlatformApplicationUserContextAccessor UserContextAccessor { get; }
    protected PlatformOutboxConfig OutboxConfig { get; }

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
        var message = BuildPlatformBusMessage<TMessage, TMessagePayload>(trackId, messagePayload, messageGroup, messageAction);

        return await SendMessageAsync(
            message,
            forceUseDefaultRoutingKey
                ? PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(message.GetType(), ApplicationSettingContext.ApplicationName)
                : message.RoutingKey(),
            autoSaveOutboxMessage,
            null,
            cancellationToken);
    }

    public async Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        bool autoSaveOutboxMessage = true,
        bool forceUseDefaultRoutingKey = false,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        return await SendMessageAsync(
            message,
            routingKey: forceUseDefaultRoutingKey || message.As<IPlatformSelfRoutingKeyBusMessage>() == null
                ? PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(message.GetType(), ApplicationSettingContext.ApplicationName)
                : message.As<IPlatformSelfRoutingKeyBusMessage>().RoutingKey(),
            autoSaveOutboxMessage,
            sourceOutboxUowId,
            cancellationToken);
    }

    public bool HasOutboxMessageSupport()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    protected PlatformBusMessageIdentity BuildPlatformEventBusMessageIdentity()
    {
        return new PlatformBusMessageIdentity
        {
            UserId = UserContextAccessor.Current.UserId(),
            RequestId = UserContextAccessor.Current.RequestId(),
            UserName = UserContextAccessor.Current.UserName()
        };
    }

    protected virtual async Task<TMessage> SendMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        bool autoSaveOutboxMessage,
        string sourceOutboxUowId,
        CancellationToken cancellationToken)
        where TMessage : class, new()
    {
        if (message is IPlatformTrackableBusMessage trackableBusMessage)
        {
            trackableBusMessage.TrackingId ??= Guid.NewGuid().ToString();
            trackableBusMessage.ProduceFrom ??= ApplicationSettingContext.ApplicationName;
            trackableBusMessage.CreatedUtcDate ??= DateTime.UtcNow;
            if (trackableBusMessage.RequestContext == null || trackableBusMessage.RequestContext.IsEmpty())
                trackableBusMessage.RequestContext = PlatformApplicationGlobal.UserContext.Current.GetAllKeyValues();
        }

        if (autoSaveOutboxMessage && HasOutboxMessageSupport())
        {
            var outboxEventBusProducerHelper = ServiceProvider.GetRequiredService<PlatformOutboxMessageBusProducerHelper>();

            await outboxEventBusProducerHelper.HandleSendingOutboxMessageAsync(
                message,
                routingKey,
                OutboxConfig.RetryProcessFailedMessageInSecondsUnit,
                handleExistingOutboxMessage: null,
                sourceOutboxUowId: sourceOutboxUowId,
                cancellationToken);

            return message;
        }

        return await MessageBusProducer.SendAsync(message, routingKey, cancellationToken);
    }

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
            requestContext: PlatformApplicationGlobal.UserContext.Current.GetAllKeyValues());
    }

    public class PlatformPseudoMessageBusProducer : IPlatformMessageBusProducer
    {
        public Task<TMessage> SendAsync<TMessage>(
            TMessage message,
            string routingKey,
            CancellationToken cancellationToken = default) where TMessage : class, new()
        {
            return Task.FromResult(message);
        }
    }
}
