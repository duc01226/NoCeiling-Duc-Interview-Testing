using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.FreeFormatConsumers;

internal sealed class DemoSendFreeFormatEventBusMessageCommandEventBusConsumer
    : PlatformApplicationMessageBusConsumer<DemoSendFreeFormatEventBusMessage>
{
    public DemoSendFreeFormatEventBusMessageCommandEventBusConsumer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider, rootServiceProvider)
    {
    }

    public override Task HandleLogicAsync(DemoSendFreeFormatEventBusMessage message, string routingKey)
    {
        // Example for sync data event update event data entity value not changed
        // if (message.ForceSyncNoCheckDiff == true)
        //    RequestContextAccessor.Current.Upsert(PlatformCqrsEntityEvent.RequestContextForceSyncDataNoCheckUpdateDataIsDifferentKey, true);

        Logger.LogInformation(
            "Message {Message} by {TargetName} has been handled",
            nameof(DemoSendFreeFormatEventBusMessage),
            GetType().Name);

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}

/// <summary>
/// Use DemoSendFreeFormatInboxEventBusMessageCommandEventBusConsumer if you need to use platform repository/use inbox messages pattern
/// </summary>
internal sealed class DemoSendFreeFormatInboxEventBusMessageCommandApplicationEventBusConsumer
    : PlatformApplicationMessageBusConsumer<DemoSendFreeFormatEventBusMessage>
{
    public DemoSendFreeFormatInboxEventBusMessageCommandApplicationEventBusConsumer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider, rootServiceProvider)
    {
    }

    public override Task HandleLogicAsync(DemoSendFreeFormatEventBusMessage message, string routingKey)
    {
        Logger.LogInformation("Message {MessageName} has been handled", nameof(DemoSendFreeFormatEventBusMessage));

        return Task.CompletedTask;
    }

    // Can override this method return false to user normal consumer without using inbox message
    //public override bool AutoSaveInboxMessage => false;
}
