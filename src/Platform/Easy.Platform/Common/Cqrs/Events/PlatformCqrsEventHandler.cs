using Easy.Platform.Application;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Constants;
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
    protected readonly ILogger Logger;

    protected PlatformCqrsEventHandler(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger($"{DefaultPlatformLogSuffix.PlatformSuffix}.{GetType().Name}");
    }

    public bool IsCurrentInstanceExecutedInSeparateThread { get; set; }

    public virtual async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            if (!HandleWhen(notification)) return;

            if (ExecuteSeparatelyInBackgroundThread() && !IsCurrentInstanceExecutedInSeparateThread)
                // Use ServiceCollection.BuildServiceProvider() to create new Root ServiceProvider
                // so the it wont be disposed when run in background thread, this handler ServiceProvider will be disposed
                Util.TaskRunner.QueueActionInBackground(
                    async () =>
                    {
                        await PlatformApplicationGlobal.RootServiceProvider
                            .ExecuteInjectScopedAsync(
                                async (IServiceProvider sp) =>
                                {
                                    var thisHandlerNewInstance = sp.GetServices<INotificationHandler<TEvent>>()
                                        .First(p => p.GetType() == GetType())
                                        .As<PlatformCqrsEventHandler<TEvent>>()
                                        .With(_ => _.IsCurrentInstanceExecutedInSeparateThread = true);

                                    await thisHandlerNewInstance.Handle(notification, default);
                                });
                    },
                    PlatformApplicationGlobal.LoggerFactory.CreateLogger($"{DefaultPlatformLogSuffix.SystemPlatformSuffix}.{GetType().Name}"),
                    cancellationToken: default);
            else
                await ExecuteHandleAsync(notification, cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError(
                e,
                "[PlatformCqrsEventHandler] Handle event failed. EventType:{EventType}; HandlerType:{HandlerType}.",
                notification.GetType().Name,
                GetType().Name);
        }
    }

    /// <summary>
    /// Default return True. Override this to define the condition to handle the event
    /// </summary>
    protected virtual bool HandleWhen(TEvent @event)
    {
        return true;
    }

    protected abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);

    protected virtual async Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        await HandleAsync(@event, cancellationToken);
    }

    /// <summary>
    /// Default is False. If true, the event handler will run in separate thread scope with new instance
    /// and if exception, it won't affect the main flow
    /// </summary>
    /// <returns></returns>
    protected virtual bool ExecuteSeparatelyInBackgroundThread()
    {
        return false;
    }
}
