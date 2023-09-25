using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.RequestContext;
using Easy.Platform.Common.Timing;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Events;

public interface IPlatformCqrsEvent : INotification
{
    string AuditTrackId { get; set; }
    DateTime CreatedDate { get; }
    string CreatedBy { get; set; }
    string EventType { get; }
    string EventName { get; }
    string EventAction { get; }
    string Id { get; }

    /// <summary>
    /// This is used to store the context of the request which generate the event, for example the CurrentUserContext
    /// </summary>
    ConcurrentDictionary<string, object> RequestContext { get; set; }

    /// <summary>
    /// Add handler type fullname If you want to force wait handler execution immediately successfully to continue. By default, handlers for entity event executing
    /// in background thread and you dont need to wait for it. The command will return immediately. <br />
    /// Sometime you could want to wait for handler done
    /// </summary>
    HashSet<string> WaitHandlerExecutionFinishedImmediatelyFullNames { get; set; }

    /// <summary>
    /// Set handler type fullname If you want to force wait handler to be handling successfully to continue. By default, handlers for entity event executing
    /// in background thread and you dont need to wait for it. The command will return immediately. <br />
    /// Sometime you could want to wait for handler done
    /// </summary>
    PlatformCqrsEvent SetWaitHandlerExecutionFinishedImmediately(params Type[] eventHandlerTypes);

    /// <inheritdoc cref="PlatformCqrsEvent.SetWaitHandlerExecutionFinishedImmediately" />
    PlatformCqrsEvent SetWaitHandlerExecutionFinishedImmediately<THandler, TEvent>()
        where THandler : IPlatformCqrsEventHandler<TEvent>
        where TEvent : PlatformCqrsEvent, new();

    bool MustWaitHandlerExecutionFinishedImmediately(Type eventHandlerType);
    T GetRequestContextValue<T>(string contextKey);
    PlatformCqrsEvent SetRequestContextValues(IDictionary<string, object> values);
    PlatformCqrsEvent SetRequestContextValue<TValue>(string key, TValue value);
}

public abstract class PlatformCqrsEvent : INotification, IPlatformCqrsEvent
{
    private readonly object initRequestContext = new();

    public string AuditTrackId { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedDate { get; } = Clock.UtcNow;

    public string CreatedBy { get; set; }

    public abstract string EventType { get; }

    public abstract string EventName { get; }

    public abstract string EventAction { get; }

    public string Id => $"{AuditTrackId}-{EventAction}";

    /// <summary>
    /// This is used to store the context of the request which generate the event, for example the CurrentUserContext
    /// </summary>
    public ConcurrentDictionary<string, object> RequestContext { get; set; }

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

    public T GetRequestContextValue<T>(string contextKey)
    {
        if (contextKey == null)
            throw new ArgumentNullException(nameof(contextKey));

        if (PlatformRequestContextHelper.TryGetValue(RequestContext, contextKey, out T item)) return item;

        throw new KeyNotFoundException($"{contextKey} not found in {nameof(RequestContext)}");
    }

    public PlatformCqrsEvent SetRequestContextValues(IDictionary<string, object> values)
    {
        InitRequestContext();

        values.ForEach(p => RequestContext.Upsert(p.Key, p.Value));

        return this;
    }

    public PlatformCqrsEvent SetRequestContextValue<TValue>(string key, TValue value)
    {
        InitRequestContext().Upsert(key, value);

        return this;
    }

    private ConcurrentDictionary<string, object> InitRequestContext()
    {
        if (RequestContext == null)
            lock (initRequestContext)
            {
                RequestContext = new ConcurrentDictionary<string, object>();
            }

        return RequestContext;
    }
}
