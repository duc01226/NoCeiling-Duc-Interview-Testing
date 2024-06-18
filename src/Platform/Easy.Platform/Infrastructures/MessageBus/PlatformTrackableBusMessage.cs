namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformTrackableBusMessage : IPlatformSubMessageQueuePrefixSupport
{
    /// <summary>
    /// Unique generated string, usually guid id to define a unique message id
    /// </summary>
    public string TrackingId { get; set; }

    public DateTime? CreatedUtcDate { get; set; }

    public string ProduceFrom { get; set; }

    public IDictionary<string, object> RequestContext { get; set; }
}

public abstract class PlatformTrackableBusMessage : IPlatformTrackableBusMessage
{
    public string TrackingId { get; set; } = Ulid.NewUlid().ToString();
    public DateTime? CreatedUtcDate { get; set; } = DateTime.UtcNow;
    public string ProduceFrom { get; set; }
    public IDictionary<string, object> RequestContext { get; set; } = new Dictionary<string, object>();

    public abstract string SubQueuePrefix();
}
