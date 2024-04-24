using Easy.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.MessageBus.Producers.EntityEventBusProducers;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.EntityEventConsumers;

/// <summary>
/// Consumer use the default routing key using message class type name without using <see cref="PlatformConsumerRoutingKeyAttribute" /> <br />
/// Must ensure TMessageClassName (TextSnippetEntityEventBusMessage) is unique in the system.
/// </summary>
// Use self routing key binding [PlatformConsumerRoutingKey(messageGroup: PlatformCqrsEntityEvent.EventTypeValue, messageType: nameof(TextSnippetEntityEventBusMessage))]
// for SendByMessageSelfRoutingKey in Producer is True
internal sealed class SnippetTextEntityEventBusConsumer : PlatformCqrsEntityEventBusMessageConsumer<TextSnippetEntityEventBusMessage, TextSnippetEntity>
{
    public SnippetTextEntityEventBusConsumer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider, rootServiceProvider)
    {
    }

    public override Task HandleLogicAsync(
        TextSnippetEntityEventBusMessage message,
        string routingKey)
    {
        Util.RandomGenerator.DoByChance(
            percentChance: 5,
            () => throw new Exception("Random Test Retry Consumer Throw Exception"));

        Logger.LogInformation(
            "{GetTypeFullName} has handled message {DomainEventsMessage}.\r\n" +
            "Message Detail: {Message}",
            GetType().FullName,
            message.Payload.DomainEvents.Any() ? $"for DomainEvents [{message.Payload.DomainEvents.Select(p => p.Key).JoinToString(", ")}]" : "",
            message.ToJson());

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}
