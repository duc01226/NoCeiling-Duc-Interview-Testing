using Easy.Platform.Common.Timing;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Events;

public abstract class PlatformCqrsEvent : INotification
{
    public string AuditTrackId { get; set; }

    public DateTime CreatedDate { get; } = Clock.Now;

    public string CreatedBy { get; set; }

    public abstract string EventType { get; }

    public abstract string EventName { get; }

    public abstract string EventAction { get; }
}
