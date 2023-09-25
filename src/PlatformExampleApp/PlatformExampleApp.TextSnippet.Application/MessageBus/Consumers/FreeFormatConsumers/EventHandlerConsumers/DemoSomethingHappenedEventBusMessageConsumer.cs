using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages.EventMessages;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.FreeFormatConsumers.EventHandlerConsumers;

/// <summary>
/// When a service support other service as a third party or common service, for example EmailService.
/// Other feature services can ask this common service to do something, then the bus message act like a request
/// The request receiver is the LEADER, control the request schema and logic.
/// Other services just use them
/// The naming convention rule is: [LEADER-SERVICE-NAME (The producer service produce event)] + XXX + EventBusMessage
/// Example: AccountServiceUserCreatedEventBusMessage
/// This is example one other feature services want to listen to an event from LEADER-SERVICE to do their own logic
/// </summary>
internal sealed class DemoSomethingHappenedEventBusMessageConsumer : PlatformApplicationMessageBusConsumer<DemoSomethingHappenedEventBusMessage>
{
    public DemoSomethingHappenedEventBusMessageConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider, rootServiceProvider)
    {
    }

    public override Task HandleLogicAsync(DemoSomethingHappenedEventBusMessage message, string routingKey)
    {
        throw new NotImplementedException();
    }
}
