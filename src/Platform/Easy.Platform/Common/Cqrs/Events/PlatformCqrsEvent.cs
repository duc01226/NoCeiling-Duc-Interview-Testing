using System.Text.Json.Serialization;
using Easy.Platform.Common.Timing;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Events;

public abstract class PlatformCqrsEvent : INotification
{
    public string AuditTrackId { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedDate { get; } = Clock.UtcNow;

    public string CreatedBy { get; set; }

    public abstract string EventType { get; }

    public abstract string EventName { get; }

    public abstract string EventAction { get; }

    public string Id => $"{AuditTrackId}-{EventAction}";

    /// <summary>
    /// Add handler type fullname If you want to force wait handler execution immediately successfully to continue. By default, handlers for entity event executing
    /// in background thread and you dont need to wait for it. The command will return immediately. <br />
    /// Sometime you could want to wait for handler done
    /// </summary>
    [JsonIgnore]
    public HashSet<string> WaitHandlerExecutionFinishedImmediatelyFullNames { get; set; }

    /// <summary>
    /// Set handler type fullname If you want to force wait handler to be handling successfully to continue. By default, handlers for entity event executing
    /// in background thread and you dont need to wait for it. The command will return immediately. <br />
    /// Sometime you could want to wait for handler done
    /// </summary>
    public virtual PlatformCqrsEvent SetWaitHandlerExecutionFinishedImmediately(params Type[] eventHandlerTypes)
    {
        WaitHandlerExecutionFinishedImmediatelyFullNames = eventHandlerTypes.Select(p => p.FullName).ToHashSet();

        return this;
    }

    /// <inheritdoc cref="SetWaitHandlerExecutionFinishedImmediately" />
    public virtual PlatformCqrsEvent SetWaitHandlerExecutionFinishedImmediately<THandler, TEvent>()
        where THandler : IPlatformCqrsEventHandler<TEvent>
        where TEvent : PlatformCqrsEvent, new()
    {
        return SetWaitHandlerExecutionFinishedImmediately(typeof(THandler));
    }

    public bool MustWaitHandlerExecutionFinishedImmediately(Type eventHandlerType)
    {
        return WaitHandlerExecutionFinishedImmediatelyFullNames?.Contains(eventHandlerType.FullName) == true;
    }
}
