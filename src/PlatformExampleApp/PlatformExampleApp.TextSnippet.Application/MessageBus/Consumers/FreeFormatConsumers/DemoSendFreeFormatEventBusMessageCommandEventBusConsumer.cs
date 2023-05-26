using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.FreeFormatConsumers;

public sealed class DemoSendFreeFormatEventBusMessageCommandEventBusConsumer
    : PlatformApplicationMessageBusConsumer<DemoSendFreeFormatEventBusMessage>
{
    public DemoSendFreeFormatEventBusMessageCommandEventBusConsumer(ILoggerFactory loggerFactory, IUnitOfWorkManager uowManager, IServiceProvider serviceProvider) : base(
        loggerFactory,
        uowManager,
        serviceProvider)
    {
    }

    public override Task HandleLogicAsync(DemoSendFreeFormatEventBusMessage message, string routingKey)
    {
        Logger.LogInformation(
            $"Message {nameof(DemoSendFreeFormatEventBusMessage)} by {GetType().Name} has been handled");

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}

/// <summary>
/// Use DemoSendFreeFormatInboxEventBusMessageCommandEventBusConsumer if you need to use platform repository/use inbox messages pattern
/// </summary>
public sealed class DemoSendFreeFormatInboxEventBusMessageCommandApplicationEventBusConsumer
    : PlatformApplicationMessageBusConsumer<DemoSendFreeFormatEventBusMessage>
{
    public DemoSendFreeFormatInboxEventBusMessageCommandApplicationEventBusConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerFactory, uowManager, serviceProvider)
    {
    }

    public override Task HandleLogicAsync(DemoSendFreeFormatEventBusMessage message, string routingKey)
    {
        Logger.LogInformation($"Message {nameof(DemoSendFreeFormatEventBusMessage)} has been handled");

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}
