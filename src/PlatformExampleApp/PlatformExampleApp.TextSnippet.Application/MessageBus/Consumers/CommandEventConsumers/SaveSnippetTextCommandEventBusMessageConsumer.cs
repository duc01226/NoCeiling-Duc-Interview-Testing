using Easy.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.CommandEventConsumers;

/// <summary>
/// The SaveSnippetTextCommandBindingDefaultRoutingKeyConsumer will throw error => Trigger message requeue => <br/>
/// Consumer use the default routing key using message class type name without using <see cref="PlatformConsumerRoutingKeyAttribute"/>
/// Must ensure TCommand class name is unique in the system.
/// </summary>
// Use self routing key binding [PlatformConsumerRoutingKey(messageGroup: PlatformCqrsCommandEvent.EventTypeValue, messageType: "PlatformCqrsCommandEvent<SaveSnippetTextCommand>")]
// for SendByMessageSelfRoutingKey in Producer is True
public class SaveSnippetTextCommandEventBusMessageConsumer : PlatformCqrsCommandEventBusMessageConsumer<SaveSnippetTextCommand>
{
    public SaveSnippetTextCommandEventBusMessageConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerFactory, uowManager, serviceProvider)
    {
    }

    // Example override this function to config this specific consumer warning time if you expect
    // a specific consumer to run fast or slow
    // In this Example we expect this consumer run less than 2 seconds (2000 milliseconds)
    public override long? SlowProcessWarningTimeMilliseconds()
    {
        return 2000;
    }

    protected override Task InternalHandleAsync(
        PlatformBusMessage<PlatformCqrsCommandEvent<SaveSnippetTextCommand>> message,
        string routingKey)
    {
        Util.RandomGenerator.DoByChance(
            percentChance: 5,
            () => throw new Exception("Random Test Retry Consumer Throw Exception"));

        // Sleep to demo warning slow consumer
        Thread.Sleep(((SlowProcessWarningTimeMilliseconds() ?? DefaultProcessWarningTimeMilliseconds) + 1000).Seconds());

        Logger.LogInformation($"{GetType().FullName} has handled message. Message Detail: {{BusMessage}}", message.ToFormattedJson());

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}
