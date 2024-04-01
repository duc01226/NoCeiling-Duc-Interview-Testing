using Easy.Platform.Common.Cqrs.Events;

namespace Easy.Platform.Common.Cqrs.Commands;

public abstract class PlatformCqrsCommandEvent : PlatformCqrsEvent
{
    public const string EventTypeValue = nameof(PlatformCqrsCommandEvent);
}

public class PlatformCqrsCommandEvent<TCommand, TCommandResult> : PlatformCqrsCommandEvent
    where TCommand : class, IPlatformCqrsCommand, new()
    where TCommandResult : class, IPlatformCqrsCommandResult, new()
{
    public PlatformCqrsCommandEvent() { }

    public PlatformCqrsCommandEvent(TCommand commandData, TCommandResult commandResult, PlatformCqrsCommandEventAction? action = null)
    {
        AuditTrackId = commandData.AuditInfo?.AuditTrackId.ToString() ?? Guid.NewGuid().ToString();
        CommandData = commandData;
        CommandResult = commandResult;
        Action = action;
    }

    public override string EventType => EventTypeValue;
    public override string EventName => typeof(TCommand).Name;
    public override string EventAction => Action?.ToString();

    public TCommand CommandData { get; set; }
    public PlatformCqrsCommandEventAction? Action { get; set; }
    public TCommandResult CommandResult { get; set; } = new();
}

public class PlatformCqrsCommandEvent<TCommand> : PlatformCqrsCommandEvent<TCommand, PlatformCqrsCommandResult>
    where TCommand : class, IPlatformCqrsCommand, new()
{
}

public enum PlatformCqrsCommandEventAction
{
    Executed
}
