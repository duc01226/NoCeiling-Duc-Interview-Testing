namespace Easy.Platform.Domain.Events;

public interface IPlatformUowEvent
{
    public string SourceUowId { get; set; }
}
