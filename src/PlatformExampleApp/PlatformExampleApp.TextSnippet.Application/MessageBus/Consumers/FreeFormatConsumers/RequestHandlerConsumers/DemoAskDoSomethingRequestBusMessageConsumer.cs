using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages.RequestMessages;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Consumers.FreeFormatConsumers.RequestHandlerConsumers;

/// <summary>
/// When a service support other service as a third party or common service, for example EmailService.
/// Other feature services can ask this common service to do something, then the bus message act like a request
/// The request receiver is the LEADER, control the request schema and logic.
/// Other services just use them
///
/// The naming convention rule is: [LEADER-SERVICE-NAME (The consumer service handle request)] + XXX + RequestBusMessage
///
/// Example: EmailServiceSendEmailRequestBusMessage
///
/// This is example the LEADER-SERVICE listen to an request message from other to do serve the request
/// </summary>
internal sealed class DemoAskDoSomethingRequestBusMessageConsumer : PlatformApplicationMessageBusConsumer<DemoAskDoSomethingRequestBusMessage>
{
    public DemoAskDoSomethingRequestBusMessageConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerFactory, uowManager, serviceProvider)
    {
    }

    public override Task HandleLogicAsync(DemoAskDoSomethingRequestBusMessage message, string routingKey)
    {
        throw new NotImplementedException();
    }
}
