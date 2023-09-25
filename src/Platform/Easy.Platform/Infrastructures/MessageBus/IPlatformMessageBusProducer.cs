using System.Diagnostics;

namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformMessageBusProducer
{
    public static readonly ActivitySource ActivitySource = new(nameof(IPlatformMessageBusProducer));

    /// <summary>
    /// Send a message to bus with a routingKey
    /// </summary>
    /// <exception cref="PlatformMessageBusException{TMessage}">Could throw if there is an exception</exception>
    public Task<TMessage> SendAsync<TMessage>(
        TMessage message,
        string routingKey,
        CancellationToken cancellationToken = default)
        where TMessage : class, new();
}
