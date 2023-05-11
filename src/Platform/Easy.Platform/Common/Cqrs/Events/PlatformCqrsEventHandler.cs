using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Cqrs.Events;

public interface IPlatformCqrsEventHandler<in TEvent> : INotificationHandler<TEvent>
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

    public virtual async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        if (!HandleWhen(notification)) return;

        try
        {
            // Use ServiceCollection.BuildServiceProvider() to create new Root ServiceProvider
            // so the it wont be disposed when run in background thread, this handler ServiceProvider will be disposed
            if (ExecuteSeparatelyInBackgroundThread())
            {
                Util.TaskRunner.QueueActionInBackground(
                    () =>
                    {
                        if (RetryOnFailedTimes > 0)
                            // Retry RetryOnFailedTimes to help resilient PlatformCqrsEventHandler. Sometime parallel, create/update concurrency could lead to error
                            return Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                                () => DoExecuteThisWithNewInstance(GetType(), notification),
                                retryCount: RetryOnFailedTimes,
                                sleepDurationProvider: retryAttempt => (retryAttempt * RetryOnFailedDelaySeconds).Seconds());
                        return DoExecuteThisWithNewInstance(GetType(), notification);
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

        static async Task DoExecuteThisWithNewInstance(Type eventHandlerType, TEvent notification)
        {
            await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                async (IServiceProvider sp) =>
                {
                    var thisHandlerNewInstance = sp.GetRequiredService(eventHandlerType)
                        .As<PlatformCqrsEventHandler<TEvent>>();

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

    /// <summary>
    /// Default is False. If true, the event handler will run in separate thread scope with new instance
    /// and if exception, it won't affect the main flow
    /// </summary>
    /// <returns></returns>
    protected virtual bool ExecuteSeparatelyInBackgroundThread()
    {
        return false;
    }

    public static ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformCqrsEventHandler<>));
    }
}
