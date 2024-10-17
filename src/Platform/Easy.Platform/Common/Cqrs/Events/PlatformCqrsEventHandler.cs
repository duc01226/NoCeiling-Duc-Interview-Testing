using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Cqrs.Events;

public interface IPlatformCqrsEventHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformCqrsEventHandler)}");

    public bool ForceCurrentInstanceHandleInCurrentThread { get; set; }

    public int RetryOnFailedTimes { get; set; }

    public bool ThrowExceptionOnHandleFailed { get; set; }

    /// <summary>
    /// Executes the handle asynchronously.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken);

    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    Task Handle(object @event, CancellationToken cancellationToken);

    public Task<bool> HandleWhen(object @event);
}

public interface IPlatformCqrsEventHandler<in TEvent> : INotificationHandler<TEvent>, IPlatformCqrsEventHandler
    where TEvent : IPlatformCqrsEvent
{
    public Task<bool> HandleWhen(TEvent @event);

    Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken);
}

public abstract class PlatformCqrsEventHandler<TEvent> : IPlatformCqrsEventHandler<TEvent>
    where TEvent : IPlatformCqrsEvent
{
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;

    private readonly Lazy<bool> isDistributedTracingEnabledLazy;

    protected PlatformCqrsEventHandler(ILoggerFactory loggerFactory, IPlatformRootServiceProvider rootServiceProvider)
    {
        LoggerFactory = loggerFactory;
        RootServiceProvider = rootServiceProvider;
        isDistributedTracingEnabledLazy = new Lazy<bool>(() => rootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true);
    }

    protected bool IsDistributedTracingEnabled => isDistributedTracingEnabledLazy.Value;

    public virtual double RetryOnFailedDelaySeconds { get; set; } = Util.TaskRunner.DefaultResilientDelaySeconds;

    /// <summary>
    /// The MustWaitHandlerExecutionFinishedImmediately method is part of the IPlatformCqrsEvent interface and its implementation is in the PlatformCqrsEvent class. This method is used to determine whether the execution of a specific event handler should be waited for to finish immediately or not.
    /// <br />
    /// In the context of the Command Query Responsibility Segregation (CQRS) pattern, this method provides a way to control the execution flow of event handlers. By default, event handlers are executed in the background and the command returns immediately without waiting for the handlers to finish. However, there might be cases where it's necessary to wait for a handler to finish its execution before proceeding, and this is where MustWaitHandlerExecutionFinishedImmediately comes into play.
    /// <br />
    /// The method takes a Type parameter, which represents the event handler type, and returns a boolean. If the method returns true, it means that the execution of the event handler of the provided type should be waited for to finish immediately.
    /// <br />
    /// In the DoHandle method of the PlatformCqrsEventHandler class, this method is used to decide whether to queue the event handler execution in the background or execute it immediately. If MustWaitHandlerExecutionFinishedImmediately returns true for the event handler type, the handler is executed immediately using the same current active uow if existing active uow; otherwise, it's queued to run in the background.
    /// </summary>
    protected virtual bool MustWaitHandlerExecutionFinishedImmediately => false;

    public virtual int RetryOnFailedTimes { get; set; } = Util.TaskRunner.DefaultResilientRetryCount;

    public bool ForceCurrentInstanceHandleInCurrentThread { get; set; }

    public virtual bool ThrowExceptionOnHandleFailed { get; set; }

    public abstract Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken);

    public abstract Task Handle(object @event, CancellationToken cancellationToken);

    public virtual Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification, cancellationToken);
    }

    public virtual async Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        if (!await HandleWhen(@event)) return;

        await ExecuteHandleWithTracingAsync(@event, () => HandleAsync(@event, cancellationToken));
    }

    public abstract Task<bool> HandleWhen(object @event);

    public abstract Task<bool> HandleWhen(TEvent @event);

    protected virtual async Task DoHandle(TEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            if (RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTraceEnabled() == true &&
                @event.StackTrace == null)
                @event.StackTrace = PlatformEnvironment.StackTrace();

            // Use ServiceCollection.BuildServiceProvider() to create new Root ServiceProvider
            // so that it wont be disposed when run in background thread, this handler ServiceProvider will be disposed
            if (AllowHandleInBackgroundThread(@event) &&
                NotNeedWaitHandlerExecutionFinishedImmediately(@event))
                Util.TaskRunner.QueueActionInBackground(
                    async () => await ExecuteHandleInNewScopeAsync(@event, cancellationToken),
                    loggerFactory: () => LoggerFactory.CreateLogger(typeof(PlatformCqrsEventHandler<>).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}"),
                    cancellationToken: default,
                    logFullStackTraceBeforeBackgroundTask: false);
            else
                await ExecuteRetryHandleAsync(this, @event);
        }
        catch (Exception e)
        {
            if (ThrowExceptionOnHandleFailed) throw;
            LogError(@event, e.BeautifyStackTrace(), LoggerFactory);
        }
    }

    protected bool NotNeedWaitHandlerExecutionFinishedImmediately(TEvent @event)
    {
        return !NeedWaitHandlerExecutionFinishedImmediately(@event);
    }

    protected bool NeedWaitHandlerExecutionFinishedImmediately(TEvent @event)
    {
        return @event.MustWaitHandlerExecutionFinishedImmediately(GetType()) || MustWaitHandlerExecutionFinishedImmediately;
    }

    /// <summary>
    /// Default is True. If true, the event handler will run in separate thread scope with new instance
    /// and if exception, it won't affect the main flow
    /// </summary>
    protected virtual bool AllowHandleInBackgroundThread(TEvent @event)
    {
        return true;
    }

    protected async Task ExecuteHandleInNewScopeAsync(
        TEvent notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await RootServiceProvider.ExecuteInjectScopedAsync(
                async (IServiceProvider sp) =>
                {
                    var thisHandlerNewInstance = sp.GetRequiredService(GetType())
                        .As<PlatformCqrsEventHandler<TEvent>>()
                        .With(newInstance => CopyPropertiesToNewInstanceBeforeExecution(this, newInstance));

                    await thisHandlerNewInstance
                        .With(p => p.ForceCurrentInstanceHandleInCurrentThread = true)
                        .Handle(notification, cancellationToken);
                });
        }
        catch (Exception e)
        {
            LogError(notification, e, LoggerFactory);
        }
    }

    protected async Task ExecuteRetryHandleAsync(
        PlatformCqrsEventHandler<TEvent> handlerNewInstance,
        TEvent notification)
    {
        try
        {
            // Retry RetryOnFailedTimes to help resilient PlatformCqrsEventHandler. Sometime parallel, create/update concurrency could lead to error
            if (RetryOnFailedTimes > 0)
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () => await handlerNewInstance.ExecuteHandleAsync(notification, default),
                    retryCount: RetryOnFailedTimes,
                    sleepDurationProvider: retryAttempt => RetryOnFailedDelaySeconds.Seconds(),
                    onRetry: (e, delayTime, retryAttempt, context) =>
                    {
                        if (retryAttempt > 2)
                            handlerNewInstance.LogError(notification, e.BeautifyStackTrace(), LoggerFactory, "Retry");
                    });
            else
                await handlerNewInstance.ExecuteHandleAsync(notification, default);
        }
        catch (Exception e)
        {
            if (ThrowExceptionOnHandleFailed) throw;
            handlerNewInstance.LogError(notification, e.BeautifyStackTrace(), LoggerFactory);
        }
    }

    protected virtual void CopyPropertiesToNewInstanceBeforeExecution(
        PlatformCqrsEventHandler<TEvent> previousInstance,
        PlatformCqrsEventHandler<TEvent> newInstance)
    {
        newInstance.ForceCurrentInstanceHandleInCurrentThread = previousInstance.ForceCurrentInstanceHandleInCurrentThread;
        newInstance.RetryOnFailedTimes = previousInstance.RetryOnFailedTimes;
    }

    protected async Task ExecuteHandleWithTracingAsync(TEvent @event, Func<Task> handleAsync)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformCqrsEventHandler.ActivitySource.StartActivity($"EventHandler.{nameof(ExecuteHandleAsync)}"))
            {
                activity?.AddTag("Type", GetType().FullName);
                activity?.AddTag("EventType", typeof(TEvent).FullName);
                activity?.AddTag("Event", @event.ToFormattedJson());

                await handleAsync();
            }
        else await handleAsync();
    }

    public virtual void LogError(TEvent notification, Exception exception, ILoggerFactory loggerFactory, string prefix = "")
    {
        CreateLogger(loggerFactory)
            .LogError(
                exception.BeautifyStackTrace(),
                "[PlatformCqrsEventHandler] {Prefix} Handle event failed. [[Message:{Message}]] [[EventType:{EventType}]]; [[HandlerType:{HandlerType}]]. [[EventContent:{EventContent}]].",
                prefix,
                exception.Message,
                notification.GetType().Name,
                GetType().Name,
                notification.ToJson());
    }

    protected abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformCqrsEventHandler<>).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}");
    }
}
