using Easy.Platform.Common.Cqrs.Events;

namespace Easy.Platform.Common.Cqrs.Commands;

public abstract class PlatformCqrsCommandEvent : PlatformCqrsEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsCommandEvent);
}

public class PlatformCqrsCommandEvent<TCommand> : PlatformCqrsCommandEvent
    where TCommand : class, IPlatformCqrsCommand, new()
{
    public PlatformCqrsCommandEvent() { }

    public PlatformCqrsCommandEvent(TCommand commandData, PlatformCqrsCommandEventAction? action = null)
    {
        AuditTrackId = commandData.AuditInfo?.AuditTrackId.ToString();
        CommandData = commandData;
        Action = action;
    }

    public override string EventType => EventTypeValue;
    public override string EventName => typeof(TCommand).Name;
    public override string EventAction => Action?.ToString();

    public TCommand CommandData { get; set; }
    public PlatformCqrsCommandEventAction? Action { get; set; }
}

public enum PlatformCqrsCommandEventAction
{
    Executed
}
