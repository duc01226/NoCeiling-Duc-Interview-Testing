using System.Text;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// Implementation to send message. Publish message to suitable exchange
/// </summary>
public class PlatformRabbitMqMessageBusProducer : IPlatformMessageBusProducer
{
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
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public async Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        string routingKey,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        try
        {
            var jsonMessage = message.AsJson();
            var selectedRoutingKey = routingKey ?? message.As<IPlatformSelfRoutingKeyBusMessage>()?.RoutingKey();

            await PublishMessageToQueueAsync(jsonMessage, selectedRoutingKey, cancellationToken);

            return message;
        }
        catch (Exception e)
        {
            throw new PlatformMessageBusException<TMessage>(message, e);
        }
    }

    private async Task PublishMessageToQueueAsync(
        string message,
        string routingKey,
        CancellationToken cancellationToken = default)
    {
        PublishMessageToQueue(message, routingKey);
    }

    private void PublishMessageToQueue(string message, string routingKey)
    {
        try
        {
            var channel = MqChannelPool.Get();

            channel.BasicPublish(
                ExchangeProvider.GetExchangeName(routingKey),
                routingKey,
                null,
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
}
