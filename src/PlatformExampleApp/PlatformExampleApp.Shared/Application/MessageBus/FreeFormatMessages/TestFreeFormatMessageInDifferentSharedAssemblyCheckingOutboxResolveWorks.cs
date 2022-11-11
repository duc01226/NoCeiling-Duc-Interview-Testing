using Easy.Platform.Infrastructures.MessageBus;

namespace PlatformExampleApp.Shared.Application.MessageBus.FreeFormatMessages;

public class TestFreeFormatMessageInDifferentSharedAssemblyCheckingOutboxResolveWorks : PlatformTrackableBusMessage
{
    public string Prop1 { get; set; } = "Prop1";
}

public class TestFreeFormatMessageInDifferentSharedAssemblyCheckingOutboxResolveWorks1 : PlatformTrackableBusMessage
{
    public string Prop1 { get; set; } = "Prop1";
}
