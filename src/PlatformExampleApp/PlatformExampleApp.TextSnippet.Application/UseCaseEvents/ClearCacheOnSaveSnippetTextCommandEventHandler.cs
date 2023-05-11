using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

public class ClearCacheOnSaveSnippetTextCommandEventHandler : PlatformCqrsCommandEventApplicationHandler<SaveSnippetTextCommand>
{
    private readonly IPlatformCacheRepositoryProvider cacheRepositoryProvider;

    public ClearCacheOnSaveSnippetTextCommandEventHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(loggerFactory, unitOfWorkManager)
    {
        this.cacheRepositoryProvider = cacheRepositoryProvider;
    }

    protected override bool ExecuteSeparatelyInBackgroundThread()
    {
        return true;
    }

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableHandleEventFromInboxBusMessage => false;

    protected override async Task HandleAsync(
        PlatformCqrsCommandEvent<SaveSnippetTextCommand> @event,
        CancellationToken cancellationToken)
    {
        // Queue task to clear cache every 5 seconds for 3 times (mean that after 5,10,15s).
        // Delay because when save snippet text, fulltext index take amount of time to update, so that we wait
        // amount of time for fulltext index update
        // We also set executeOnceImmediately=true to clear cache immediately in case of some index is updated fast
        Util.TaskRunner.QueueIntervalAsyncActionInBackground(
            token => cacheRepositoryProvider.Get()
                .RemoveCollectionAsync<TextSnippetCollectionCacheKeyProvider>(token),
            intervalTimeInSeconds: 5,
            CreateGlobalLogger,
            maximumIntervalExecutionCount: 3,
            executeOnceImmediately: true,
            cancellationToken);

        // In other service if you want to run something in the background thread with scope, follow this example
        // Util.TaskRunner.QueueAsyncActionInBackground(
        //             token => PlatformRootServiceProvider.RootServiceProvider
        //                 .ExecuteInjectScopedAsync(
        //                     (IPlatformCacheRepositoryProvider cacheRepositoryProvider) =>
        //                     {
        //                         cacheRepositoryProvider.Get().RemoveCollectionAsync[TextSnippetCollectionCacheKeyProvider](token);
        //                     }),
        //             TimeSpan.Zero,
        //             logger,
        //             cancellationToken);
    }
}
