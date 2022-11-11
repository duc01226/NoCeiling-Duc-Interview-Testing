namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformSelfRoutingKeyBusMessage : IPlatformMessage
{
    public PlatformBusMessageIdentity Identity { get; set; }

    public string MessageGroup { get; set; }

    public string ProducerContext { get; set; }

    public string MessageType { get; set; }

    public string MessageAction { get; set; }

    public PlatformBusMessageRoutingKey RoutingKey();
}
