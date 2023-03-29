using Easy.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Events;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.DomainEventConsumers;

/// <summary>
/// Consumer use the default routing key using message class type name without using <see cref="PlatformConsumerRoutingKeyAttribute"/>
/// Must ensure TDomainEvent class name (TransferSnippetTextToMultiDbDemoEntityNameDomainEvent) is unique in the system.
/// </summary>
// Use self routing key binding [PlatformConsumerRoutingKey(messageGroup: PlatformCqrsDomainEvent.EventTypeValue, messageType: nameof(TransferSnippetTextToMultiDbDemoEntityNameDomainEvent))]
// for SendByMessageSelfRoutingKey in Producer is True
public class TransferSnippetTextToMultiDbDemoEntityNameDomainEventConsumer
    : PlatformCqrsDomainEventBusMessageConsumer<TransferSnippetTextToMultiDbDemoEntityNameDomainEvent>
{
    public TransferSnippetTextToMultiDbDemoEntityNameDomainEventConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerFactory, uowManager, serviceProvider)
    {
    }

    protected override Task InternalHandleAsync(
        PlatformBusMessage<TransferSnippetTextToMultiDbDemoEntityNameDomainEvent> message,
        string routingKey)
    {
        Util.Random.DoByChance(
            percentChance: 5,
            () => throw new Exception("Random Test Retry Consumer Throw Exception"));

        Logger.LogInformation(
            $"{GetType().FullName} has handled message. Message Detail: {{BusMessage}}",
            message.ToFormattedJson());

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}
