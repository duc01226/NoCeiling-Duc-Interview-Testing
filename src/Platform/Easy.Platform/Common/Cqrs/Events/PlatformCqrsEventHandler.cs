using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Cqrs.Events;

public interface IPlatformCqrsEventHandler
{
    public bool ForceCurrentInstanceHandleInCurrentThread { get; set; }
}

public interface IPlatformCqrsEventHandler<in TEvent> : INotificationHandler<TEvent>, IPlatformCqrsEventHandler
    where TEvent : PlatformCqrsEvent, new()
{
}

public abstract class PlatformCqrsEventHandler<TEvent> : IPlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    protected readonly ILoggerFactory LoggerFactory;

    protected PlatformCqrsEventHandler(ILoggerFactory loggerFactory)
    {
        LoggerFactory = loggerFactory;
    }

    public virtual int RetryOnFailedTimes => 2;

    public virtual double RetryOnFailedDelaySeconds => 1;

    /// <summary>
    /// Default is True. If true, the event handler will run in separate thread scope with new instance
    /// and if exception, it won't affect the main flow
    /// </summary>
    protected virtual bool EnableHandleInBackgroundThread => true;

    public bool ForceCurrentInstanceHandleInCurrentThread { get; set; }

    public virtual async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        if (!HandleWhen(notification)) return;

        try
        {
            // Use ServiceCollection.BuildServiceProvider() to create new Root ServiceProvider
            // so the it wont be disposed when run in background thread, this handler ServiceProvider will be disposed
            if (EnableHandleInBackgroundThread && !ForceCurrentInstanceHandleInCurrentThread)
            {
                // Need to get current data context outside of QueueActionInBackground to has data because it read current context by thread. If inside => other threads => lose identity data
                var currentDataContextAllValues = BuildDataContextBeforeBackgroundExecution();

                Util.TaskRunner.QueueActionInBackground(
                    () =>
                    {
                        // Retry RetryOnFailedTimes to help resilient PlatformCqrsEventHandler. Sometime parallel, create/update concurrency could lead to error
                        if (RetryOnFailedTimes > 0)
                            return Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                                () => DoExecuteNewInstanceInBackgroundThread(this, GetType(), notification, currentDataContextAllValues),
                                retryCount: RetryOnFailedTimes,
                                sleepDurationProvider: retryAttempt => (retryAttempt * RetryOnFailedDelaySeconds).Seconds());

                        return DoExecuteNewInstanceInBackgroundThread(this, GetType(), notification, currentDataContextAllValues);
                    },
                    () => PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformCqrsEventHandler<>)),
                    cancellationToken: default);
            }
            else
            {
                if (RetryOnFailedTimes > 0)
                    // Retry RetryOnFailedTimes to help resilient PlatformCqrsEventHandler. Sometime parallel, create/update concurrency could lead to error
                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        () => ExecuteHandleAsync(notification, cancellationToken),
                        retryCount: RetryOnFailedTimes,
                        sleepDurationProvider: retryAttempt => (retryAttempt * RetryOnFailedDelaySeconds).Seconds());
                else
                    await ExecuteHandleAsync(notification, cancellationToken);
            }
        }
        catch (Exception e)
        {
            LogError(notification, e, LoggerFactory);
        }
    }

    protected async Task DoExecuteNewInstanceInBackgroundThread(
        PlatformCqrsEventHandler<TEvent> previousInstance,
        Type eventHandlerType,
        TEvent notification,
        Dictionary<string, object> currentDataContextAllValues)
    {
        await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
            async (IServiceProvider sp) =>
            {
                var thisHandlerNewInstance = sp.GetRequiredService(eventHandlerType)
                    .As<PlatformCqrsEventHandler<TEvent>>()
                    .With(newInstance => CopyValuesToNewInstanceInBackgroundBeforeExecution(previousInstance, newInstance))
                    .With(newInstance => newInstance.ReApplyDataContextInBackgroundExecution(sp, currentDataContextAllValues));

                try
                {
                    await thisHandlerNewInstance.ExecuteHandleAsync(notification, default);
                }
                catch (Exception e)
                {
                    thisHandlerNewInstance.LogError(notification, e, PlatformGlobal.LoggerFactory);
                }
            });
    }

    protected virtual void CopyValuesToNewInstanceInBackgroundBeforeExecution(
        PlatformCqrsEventHandler<TEvent> previousInstance,
        PlatformCqrsEventHandler<TEvent> newInstance)
    {
        newInstance.ForceCurrentInstanceHandleInCurrentThread = previousInstance.ForceCurrentInstanceHandleInCurrentThread;
    }

    /// <summary>
    /// Help for any inherit handler could override BuildDataContextBeforeBackgroundExecution/ReApplyDataContextInBackgroundExecution to handle custom additional
    /// set context values which is different per thread
    /// </summary>
    protected virtual Dictionary<string, object> BuildDataContextBeforeBackgroundExecution()
    {
        return null;
    }

    /// <summary>
    /// Help for any inherit handler could override BuildDataContextBeforeBackgroundExecution/ReApplyDataContextInBackgroundExecution to handle custom additional
    /// set context values which is different per thread
    /// </summary>
    protected virtual void ReApplyDataContextInBackgroundExecution(
        IServiceProvider inBackgroundServiceProvider,
        Dictionary<string, object> dataContextBeforeBackgroundExecution)
    {
        // Default do not thing here.
    }

    public virtual Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        return HandleAsync(@event, cancellationToken);
    }

    public virtual void LogError(TEvent notification, Exception exception, ILoggerFactory loggerFactory)
    {
        CreateLogger(loggerFactory)
            .LogError(
                exception,
                "[PlatformCqrsEventHandler] Handle event failed: {ExceptionMessage}. EventType:{EventType}; HandlerType:{HandlerType}. EventContent:{EventContent}",
                exception.Message,
                notification.GetType().Name,
                GetType().Name,
                notification.ToJson());
    }

    /// <summary>
    /// Default return True. Override this to define the condition to handle the event
    /// </summary>
    protected virtual bool HandleWhen(TEvent @event)
    {
        return true;
    }

    protected abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);

    public static ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformCqrsEventHandler<>));
    }
}
