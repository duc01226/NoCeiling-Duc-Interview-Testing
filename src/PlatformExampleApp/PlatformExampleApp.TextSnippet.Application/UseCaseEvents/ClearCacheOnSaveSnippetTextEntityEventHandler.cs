using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

internal sealed class ClearCacheOnSaveSnippetTextEntityEventHandler : PlatformCqrsEntityEventApplicationHandler<TextSnippetEntity>
{
    private readonly IPlatformCacheRepositoryProvider cacheRepositoryProvider;

    public ClearCacheOnSaveSnippetTextEntityEventHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(loggerFactory, unitOfWorkManager, serviceProvider)
    {
        this.cacheRepositoryProvider = cacheRepositoryProvider;
    }

    // Demo can override to config either this handler run in a background thread
    protected override bool AllowHandleInBackgroundThread(PlatformCqrsEntityEvent<TextSnippetEntity> notification)
    {
        return true;
    }

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableHandleEventFromInboxBusMessage => false;

    protected override async Task HandleAsync(
        PlatformCqrsEntityEvent<TextSnippetEntity> @event,
        CancellationToken cancellationToken)
    {
        // Test slow event do not affect main command
        await Task.Delay(5.Seconds(), cancellationToken);

        Util.RandomGenerator.DoByChance(percentChance: 50, () => throw new Exception("Test throw exception in event handler"));

        // Queue task to clear cache every 5 seconds for 3 times (mean that after 5,10,15s).
        // Delay because when save snippet text, fulltext index take amount of time to update, so that we wait
        // amount of time for fulltext index update
        // We also set executeOnceImmediately=true to clear cache immediately in case of some index is updated fast
        Util.TaskRunner.QueueIntervalAsyncActionInBackground(
            token => cacheRepositoryProvider.Get().RemoveCollectionAsync<TextSnippetCollectionCacheKeyProvider>(token),
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
