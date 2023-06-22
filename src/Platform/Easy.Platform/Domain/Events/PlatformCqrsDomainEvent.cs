using Easy.Platform.Common.Cqrs.Events;

namespace Easy.Platform.Domain.Events;

public abstract class PlatformCqrsDomainEvent : PlatformCqrsEvent, IPlatformUowEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsDomainEvent);

    public override string EventType => EventTypeValue;

    public override string EventName => GetType().Name;

    public override string EventAction => null;

    public string SourceUowId { get; set; }
}
