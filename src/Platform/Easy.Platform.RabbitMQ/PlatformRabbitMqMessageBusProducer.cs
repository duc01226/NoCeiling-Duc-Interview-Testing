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
    public static readonly TextMapPropagator TracingActivityPropagator = Propagators.DefaultTextMapPropagator;

    protected readonly PlatformProducerRabbitMqChannelPool ChannelPool;
    protected readonly IPlatformRabbitMqExchangeProvider ExchangeProvider;
    protected readonly Lazy<ILogger> Logger;
    protected readonly PlatformRabbitMqOptions Options;

    public PlatformRabbitMqMessageBusProducer(
        IPlatformRabbitMqExchangeProvider exchangeProvider,
        PlatformRabbitMqOptions options,
        ILoggerFactory loggerFactory,
        PlatformProducerRabbitMqChannelPool channelPool)
    {
        ChannelPool = channelPool;
        ExchangeProvider = exchangeProvider;
        Options = options;
        Logger = new Lazy<ILogger>(() => loggerFactory.CreateLogger(typeof(IPlatformMessageBusProducer).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}"));
    }

    public async Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        string routingKey,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        try
        {
            var jsonMessage = message.ToJson(forceUseRuntimeType: true);
            var selectedRoutingKey = routingKey ?? message.As<IPlatformSelfRoutingKeyBusMessage>()?.RoutingKey();

            await PublishMessageToQueueAsync(jsonMessage, selectedRoutingKey);

            return message;
        }
        catch (Exception e)
        {
            throw new PlatformMessageBusException<TMessage>(message, e);
        }
    }

    private Task PublishMessageToQueueAsync(
        string message,
        string routingKey)
    {
        PublishMessageToQueue(message, routingKey);

        return Task.CompletedTask;
    }

    private void PublishMessageToQueue(string message, string routingKey)
    {
        using (var activity = IPlatformMessageBusProducer.ActivitySource.StartActivity(
            $"MessageBusProducer.{nameof(IPlatformMessageBusProducer.SendAsync)}",
            ActivityKind.Producer))
        {
            activity?.AddTag("routingKey", routingKey);
            activity?.AddTag("message", message);

            IModel channel = null;

            try
            {
                channel = ChannelPool.Get();

                var publishRequestProps = channel.CreateBasicProperties();
                publishRequestProps.Persistent = true;

                InjectDistributedTracingInfoIntoRequestProps(activity, publishRequestProps);

                channel.BasicPublish(
                    ExchangeProvider.GetExchangeName(routingKey),
                    routingKey,
                    publishRequestProps,
                    body: Encoding.UTF8.GetBytes(message));

                ChannelPool.Return(channel);
            }
            catch (AlreadyClosedException alreadyClosedException)
            {
                if (alreadyClosedException.ShutdownReason.ReplyCode == 404)
                    Logger.Value.LogWarning(
                        "Tried to send a message with routing key {RoutingKey} from {ProducerType} " +
                        "but exchange is not found. May be there is no consumer registered to consume this message." +
                        "If in source code has consumers for this message, this could be unexpected errors",
                        routingKey,
                        GetType().FullName);
                else
                    throw;
            }
            finally
            {
                if (channel != null) ChannelPool.Return(channel);
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
                Logger.Value.LogError(ex.BeautifyStackTrace(), "Failed to inject trace context.");
            }
        }
    }
}
