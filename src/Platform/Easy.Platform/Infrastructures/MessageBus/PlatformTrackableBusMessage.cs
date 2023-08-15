namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformTrackableBusMessage : IPlatformMessage
{
    public string TrackingId { get; set; }

    public DateTime? CreatedUtcDate { get; set; }

    public string ProduceFrom { get; set; }

    public Dictionary<string, object> RequestContext { get; set; }
}

public class PlatformTrackableBusMessage : IPlatformTrackableBusMessage
{
    public string TrackingId { get; set; } = Guid.NewGuid().ToString();
    public DateTime? CreatedUtcDate { get; set; } = DateTime.UtcNow;
    public string ProduceFrom { get; set; }
    public Dictionary<string, object> RequestContext { get; set; } = new();
}
