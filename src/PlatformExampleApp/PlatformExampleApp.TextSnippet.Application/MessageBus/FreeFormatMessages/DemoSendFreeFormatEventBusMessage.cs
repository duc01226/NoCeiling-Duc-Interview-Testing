using Easy.Platform.Infrastructures.MessageBus;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages;

public class DemoSendFreeFormatEventBusMessage : PlatformTrackableBusMessage
{
    public string Property1 { get; set; }
    public int Property2 { get; set; }
}
