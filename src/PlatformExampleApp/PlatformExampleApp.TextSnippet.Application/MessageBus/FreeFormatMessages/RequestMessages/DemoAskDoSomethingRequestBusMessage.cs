using Easy.Platform.Infrastructures.MessageBus;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages.RequestMessages;

/// <summary>
/// When a service support other service as a third party or common service, for example EmailService.
/// Other feature services can ask this common service to do something, then the bus message act like a request
/// The request receiver is the LEADER, control the request schema and logic.
/// Other services just use them
///
/// The naming convention rule is: [LEADER-SERVICE-NAME (The consumer service handle request)] + XXX + RequestBusMessage
///
/// Example: EmailServiceSendEmailRequestBusMessage
/// </summary>
public class DemoAskDoSomethingRequestBusMessage : PlatformTrackableBusMessage
{
}
