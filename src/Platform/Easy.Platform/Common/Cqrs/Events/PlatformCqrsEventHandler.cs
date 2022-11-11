using Easy.Platform.Application;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Cqrs.Events;

public interface IPlatformCqrsEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    public void MarkCurrentInstanceIsExecutedInSeparateThread();
    public bool IsCurrentInstanceExecutedInSeparateThread();
}

public abstract class PlatformCqrsEventHandler<TEvent> : IPlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    protected readonly ILogger Logger;
    private bool isCurrentInstanceExecutedInSeparateThread;

    protected PlatformCqrsEventHandler(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public virtual async Task Handle(TEvent request, CancellationToken cancellationToken)
    {
        if (ExecuteSeparatelyInBackgroundThread() && !IsCurrentInstanceExecutedInSeparateThread())
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
                                    .As<IPlatformCqrsEventHandler<TEvent>>()
                                    .With(_ => _.MarkCurrentInstanceIsExecutedInSeparateThread());

                                await thisHandlerNewInstance.Handle(request, default);
                            });
                },
                PlatformApplicationGlobal.RootServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()),
                cancellationToken: default);
        else
            await ExecuteHandleAsync(request, cancellationToken);
    }

    public void MarkCurrentInstanceIsExecutedInSeparateThread()
    {
        isCurrentInstanceExecutedInSeparateThread = true;
    }

    public bool IsCurrentInstanceExecutedInSeparateThread()
    {
        return isCurrentInstanceExecutedInSeparateThread;
    }

    protected abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);

    protected virtual async Task ExecuteHandleAsync(TEvent request, CancellationToken cancellationToken)
    {
        await HandleAsync(request, cancellationToken);
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
