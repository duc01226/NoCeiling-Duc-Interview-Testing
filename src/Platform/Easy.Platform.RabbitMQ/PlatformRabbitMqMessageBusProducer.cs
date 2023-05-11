using System.Diagnostics;
using System.Text;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// Implementation to send message. Publish message to suitable exchange
/// </summary>
public class PlatformRabbitMqMessageBusProducer : IPlatformMessageBusProducer
{
    public static readonly ActivitySource ActivitySource = new(nameof(PlatformRabbitMqMessageBusProducer));
    public static readonly TextMapPropagator TracingActivityPropagator = Propagators.DefaultTextMapPropagator;

    protected readonly IPlatformRabbitMqExchangeProvider ExchangeProvider;
    protected readonly ILogger Logger;
    protected readonly PlatformRabbitMqChannelPool MqChannelPool;
    protected readonly PlatformRabbitMqOptions Options;

    public PlatformRabbitMqMessageBusProducer(
        IPlatformRabbitMqExchangeProvider exchangeProvider,
        PlatformRabbitMqOptions options,
        ILoggerFactory loggerFactory,
        PlatformRabbitMqChannelPool mqChannelPool)
    {
        MqChannelPool = mqChannelPool;
        ExchangeProvider = exchangeProvider;
        Options = options;
        Logger = loggerFactory.CreateLogger(typeof(PlatformRabbitMqMessageBusProducer));
    }

    public async Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        string routingKey,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        try
        {
            var jsonMessage = message.ToJson();
            var selectedRoutingKey = routingKey ?? message.As<IPlatformSelfRoutingKeyBusMessage>()?.RoutingKey();

            await PublishMessageToQueueAsync(jsonMessage, selectedRoutingKey);

            return message;
        }
        catch (Exception e)
        {
            throw new PlatformMessageBusException<TMessage>(message, e);
        }
    }

    private async Task PublishMessageToQueueAsync(
        string message,
        string routingKey)
    {
        PublishMessageToQueue(message, routingKey);
    }

    private void PublishMessageToQueue(string message, string routingKey)
    {
        using (var activity = ActivitySource.StartActivity($"{nameof(PlatformRabbitMqMessageBusProducer)}.{nameof(PublishMessageToQueue)}", ActivityKind.Producer))
        {
            activity?.AddTag("routingKey", routingKey);
            activity?.AddTag("message", message);

            try
            {
                var channel = MqChannelPool.Get();

                var publishRequestProps = channel.CreateBasicProperties();

                InjectDistributedTracingInfoIntoRequestProps(activity, publishRequestProps);

                channel.BasicPublish(
                    ExchangeProvider.GetExchangeName(routingKey),
                    routingKey,
                    publishRequestProps,
                    body: Encoding.UTF8.GetBytes(message));

                channel.Close();
            }
            catch (AlreadyClosedException alreadyClosedException)
            {
                if (alreadyClosedException.ShutdownReason.ReplyCode == 404)
                    Logger.LogWarning(
                        $"Tried to send a message with routing key {routingKey} from {GetType().FullName} " +
                        "but exchange is not found. May be there is no consumer registered to consume this message." +
                        "If in source code has consumers for this message, this could be unexpected errors");
                else
                    throw;
            }
        }

        // This help consumer can extract tracing information for continuing tracing
        void InjectDistributedTracingInfoIntoRequestProps(Activity activity, IBasicProperties publishRequestProps)
        {
            if (activity != null)
                TracingActivityPropagator.Inject(
                    new PropagationContext(activity.Context, Baggage.Current),
                    publishRequestProps,
                    InjectDistributedTracingContextIntoSendMessageRequestHeader);
        }

        void InjectDistributedTracingContextIntoSendMessageRequestHeader(IBasicProperties props, string key, string value)
        {
            try
            {
                props.Headers ??= new Dictionary<string, object>();
                props.Headers[key] = value;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to inject trace context.");
            }
        }
    }
}
