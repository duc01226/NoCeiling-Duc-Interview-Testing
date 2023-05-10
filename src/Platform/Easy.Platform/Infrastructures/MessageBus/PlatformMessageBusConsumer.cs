using System.Text.Json;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common.Exceptions.Extensions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformMessageBusConsumer
{
    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }

    /// <summary>
    /// Config the time in milliseconds to log warning if the process consumer time is over ProcessWarningTimeMilliseconds.
    /// </summary>
    long? SlowProcessWarningTimeMilliseconds();

    bool DisableSlowProcessWarning();

    JsonSerializerOptions CustomJsonSerializerOptions();

    /// <summary>
    /// Default is 0. Return bigger number order to execute it later by order ascending
    /// </summary>
    int ExecuteOrder();

    public static PlatformBusMessageRoutingKey BuildForConsumerDefaultBindingRoutingKey(Type consumerType)
    {
        var messageType = GetConsumerMessageType(consumerType);

        return PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(messageType);
    }

    public static Type GetConsumerMessageType(Type consumerGenericType)
    {
        return consumerGenericType.GetGenericArguments()[0];
    }
}

public interface IPlatformMessageBusConsumer<in TMessage> : IPlatformMessageBusConsumer
    where TMessage : class, new()
{
    /// <summary>
    /// Main Entry Handle Method
    /// </summary>
    Task HandleAsync(TMessage message, string routingKey);

    /// <summary>
    /// Main handle logic only method of the consumer
    /// </summary>
    Task HandleLogicAsync(TMessage message, string routingKey);
}

public abstract class PlatformMessageBusConsumer : IPlatformMessageBusConsumer
{
    public const long DefaultProcessWarningTimeMilliseconds = 5000;

    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }

    public virtual long? SlowProcessWarningTimeMilliseconds()
    {
        return DefaultProcessWarningTimeMilliseconds;
    }

    public virtual bool DisableSlowProcessWarning()
    {
        return false;
    }

    public virtual JsonSerializerOptions CustomJsonSerializerOptions()
    {
        return null;
    }

    public virtual int ExecuteOrder()
    {
        return 0;
    }

    /// <summary>
    /// Get <see cref="PlatformBusMessage{TPayload}" /> concrete message type from a <see cref="IPlatformMessageBusConsumer" /> consumer
    /// <br />
    /// Get a generic type: PlatformEventBusMessage{TMessage} where TMessage = TMessagePayload
    /// of IPlatformEventBusConsumer{TMessagePayload}
    /// </summary>
    public static Type GetConsumerMessageType(IPlatformMessageBusConsumer consumer)
    {
        var consumerGenericType = consumer
            .GetType()
            .GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IPlatformMessageBusConsumer<>));

        // WHY: To ensure that the consumer implements the correct interface IPlatformEventBusConsumer<> OR IPlatformEventBusCustomMessageConsumer<>.
        // The IPlatformEventBusConsumer (non-generic version) is used for Interface Marker only.
        if (consumerGenericType == null)
            throw new Exception("Must be implementation of IPlatformMessageBusConsumer<>");

        return IPlatformMessageBusConsumer.GetConsumerMessageType(consumerGenericType);
    }

    public static async Task InvokeConsumerAsync(
        IPlatformMessageBusConsumer consumer,
        object busMessage,
        string routingKey,
        bool isLogConsumerProcessTime,
        double slowProcessWarningTimeMilliseconds = DefaultProcessWarningTimeMilliseconds,
        ILogger logger = null,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation(
            "[MessageBus] Start invoking consumer. Name: {ConsumerName}. RoutingKey: {RoutingKey}. TrackingId: {TrackingId}",
            consumer.GetType().FullName,
            routingKey,
            busMessage.As<IPlatformTrackableBusMessage>()?.TrackingId ?? "n/a");

        if (isLogConsumerProcessTime && !consumer.DisableSlowProcessWarning())
            await Util.TaskRunner.ProfileExecutionAsync(
                asyncTask: async () => await DoInvokeConsumer(consumer, busMessage, routingKey, cancellationToken),
                afterExecution: elapsedMilliseconds =>
                {
                    var logMessage =
                        $"ElapsedMilliseconds:{elapsedMilliseconds}. Consumer:{consumer.GetType().FullName}. RoutingKey:{routingKey}. TrackingId:{busMessage.As<IPlatformTrackableBusMessage>()?.TrackingId ?? "n/a"}.";

                    var toCheckSlowProcessWarningTimeMilliseconds = consumer.SlowProcessWarningTimeMilliseconds() ??
                                                                    slowProcessWarningTimeMilliseconds;
                    if (elapsedMilliseconds >= toCheckSlowProcessWarningTimeMilliseconds)
                        logger?.LogWarning(
                            $"[MessageBus] SlowProcessWarningTimeMilliseconds:{toCheckSlowProcessWarningTimeMilliseconds}. {logMessage}. MessageContent: {{BusMessage}}",
                            busMessage.ToJson());
                });
        else
            await DoInvokeConsumer(
                consumer,
                busMessage,
                routingKey,
                cancellationToken);

        logger?.LogInformation(
            "[MessageBus] Finished invoking consumer. Name: {ConsumerName}. RoutingKey: {RoutingKey}. TrackingId: {TrackingId}",
            consumer.GetType().FullName,
            routingKey,
            busMessage.As<IPlatformTrackableBusMessage>()?.TrackingId ?? "n/a");
    }

    private static async Task DoInvokeConsumer(
        IPlatformMessageBusConsumer consumer,
        object eventBusMessage,
        string routingKey,
        CancellationToken cancellationToken)
    {
        var handleMethodName = nameof(IPlatformMessageBusConsumer<object>.HandleAsync);

        var methodInfo = consumer.GetType()
            .GetMethod(handleMethodName)
            .EnsureFound($"Can not find execution handle method {handleMethodName} from {consumer.GetType().FullName}");

        try
        {
            var invokeResult = methodInfo.Invoke(
                consumer,
                Util.ListBuilder.NewArray(eventBusMessage, routingKey));

            if (invokeResult is Task invokeTask) await invokeTask;
        }
        catch (Exception e)
        {
            throw new PlatformInvokeConsumerException(e, consumer.GetType().FullName, eventBusMessage);
        }
    }
}

public abstract class PlatformMessageBusConsumer<TMessage> : PlatformMessageBusConsumer, IPlatformMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
    protected readonly ILogger Logger;

    public PlatformMessageBusConsumer(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger($"{DefaultPlatformMessageBusLogSuffix.Value}.{GetType().Name}");
    }

    public virtual async Task HandleAsync(TMessage message, string routingKey)
    {
        try
        {
            await HandleLogicAsync(message, routingKey);
        }
        catch (Exception e)
        {
            Logger.LogError(
                e,
                $"Error Consume message [RoutingKey:{{RoutingKey}}], [Type:{{MessageName}}].{Environment.NewLine}" +
                $"Message Info: {{BusMessage}}.{Environment.NewLine}",
                routingKey,
                message.GetType().GetNameOrGenericTypeName(),
                message.ToJson());
            throw;
        }
    }

    public abstract Task HandleLogicAsync(TMessage message, string routingKey);
}
