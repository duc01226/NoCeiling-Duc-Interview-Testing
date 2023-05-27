using Easy.Platform.Infrastructures.MessageBus;

namespace PlatformExampleApp.Shared.Application.MessageBus.FreeFormatMessages;

public sealed class TestFreeFormatMessageInDifferentSharedAssemblyCheckingOutboxResolveWorks : PlatformTrackableBusMessage
{
    public string Prop1 { get; set; } = "Prop1";
}

public sealed class TestFreeFormatMessageInDifferentSharedAssemblyCheckingOutboxResolveWorks1 : PlatformTrackableBusMessage
{
    public string Prop1 { get; set; } = "Prop1";
}
