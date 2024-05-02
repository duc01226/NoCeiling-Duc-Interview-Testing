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
    /// <remarks>
    /// The SetWaitHandlerExecutionFinishedImmediately method in the PlatformCqrsEvent class is used to specify event handlers that should be waited for to finish execution before continuing.
    /// <br />
    /// By default, event handlers in this system are executed in a background thread and the command returns immediately without waiting for the handlers to finish. This is typically desirable for maintaining responsiveness in the system.
    /// <br />
    /// However, there might be cases where it's necessary to ensure that a specific handler has completed its task before proceeding. This could be due to dependencies between handlers, or the need to ensure that a certain operation has been completed before sending a response.
    /// <br />
    /// In such cases, you can use SetWaitHandlerExecutionFinishedImmediately to specify the handlers that should be waited for. The method accepts an array of Type objects, which represent the handlers to wait for. The full names of these types are stored in a HashSet for quick lookup later.
    /// <br />
    /// There are two versions of this method. The first accepts an array of Type objects directly, while the second is a generic method that accepts type parameters for the handler and event types. This second version simply wraps the first, providing a more type-safe way to specify the handlers.
    /// <br />
    /// In summary, SetWaitHandlerExecutionFinishedImmediately provides a way to control the execution flow of event handlers in a CQRS system, allowing you to ensure that certain handlers have completed before proceeding.
    /// </remarks>
    PlatformCqrsEvent SetWaitHandlerExecutionFinishedImmediately(params Type[] eventHandlerTypes);

    /// <inheritdoc cref="SetWaitHandlerExecutionFinishedImmediately" />
    PlatformCqrsEvent SetWaitHandlerExecutionFinishedImmediately<THandler, TEvent>()
        where THandler : IPlatformCqrsEventHandler<TEvent>
        where TEvent : PlatformCqrsEvent, new();

    /// <summary>
    /// The MustWaitHandlerExecutionFinishedImmediately method is part of the IPlatformCqrsEvent interface and its implementation is in the PlatformCqrsEvent class. This method is used to determine whether the execution of a specific event handler should be waited for to finish immediately or not.
    /// <br />
    /// In the context of the Command Query Responsibility Segregation (CQRS) pattern, this method provides a way to control the execution flow of event handlers. By default, event handlers are executed in the background and the command returns immediately without waiting for the handlers to finish. However, there might be cases where it's necessary to wait for a handler to finish its execution before proceeding, and this is where MustWaitHandlerExecutionFinishedImmediately comes into play.
    /// <br />
    /// The method takes a Type parameter, which represents the event handler type, and returns a boolean. If the method returns true, it means that the execution of the event handler of the provided type should be waited for to finish immediately.
    /// <br />
    /// In the DoHandle method of the PlatformCqrsEventHandler class, this method is used to decide whether to queue the event handler execution in the background or execute it immediately. If MustWaitHandlerExecutionFinishedImmediately returns true for the event handler type, the handler is executed immediately; otherwise, it's queued to run in the background.
    /// </summary>
    bool MustWaitHandlerExecutionFinishedImmediately(Type eventHandlerType);

    T GetRequestContextValue<T>(string contextKey);

    /// <summary>
    /// The SetRequestContextValues method in the IPlatformCqrsEvent interface is used to set the context for a given request. This method accepts a dictionary of key-value pairs, where the key is a string and the value is an object.
    /// <br />
    /// In the context of CQRS (Command Query Responsibility Segregation), this method can be used to pass additional data or metadata that might be needed during the processing of the event. This could include information such as the user who initiated the event, the timestamp when the event was created, or any other contextual data that might be relevant.
    /// <br />
    /// The method returns an instance of PlatformCqrsEvent, allowing for method chaining. This means you can call this method in conjunction with other methods in a single statement.
    /// </summary>
    PlatformCqrsEvent SetRequestContextValues(IDictionary<string, object> values);

    /// <inheritdoc cref="SetRequestContextValues" />
    PlatformCqrsEvent SetRequestContextValue<TValue>(string key, TValue value);
}

public abstract class PlatformCqrsEvent : IPlatformCqrsEvent
{
    private readonly object initRequestContextLock = new();

    public Dictionary<string, object> AdditionalMetadata { get; set; } = [];

    public string AuditTrackId { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedDate { get; } = Clock.UtcNow;

    public string CreatedBy { get; set; }

    public abstract string EventType { get; }

    public abstract string EventName { get; }

    public abstract string EventAction { get; }

    public string Id => $"{EventAction}-{AuditTrackId}";

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
        ArgumentNullException.ThrowIfNull(contextKey);

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
            lock (initRequestContextLock)
            {
                RequestContext = new ConcurrentDictionary<string, object>();
            }

        return RequestContext;
    }
}
