using Easy.Platform.Application;
using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

internal sealed class ClearCacheOnSaveSnippetTextEntityEventHandler : PlatformCqrsEntityEventApplicationHandler<TextSnippetEntity>
{
    public ClearCacheOnSaveSnippetTextEntityEventHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider) : base(loggerFactory, unitOfWorkManager, serviceProvider)
    {
    }

    // Demo can override to config either this handler run in a background thread
    protected override bool AllowHandleInBackgroundThread(PlatformCqrsEntityEvent<TextSnippetEntity> @event)
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

        var removeFilterRequestCacheKeyParts = SearchSnippetTextQuery.BuildCacheRequestKeyParts(request: null, userId: null, companyId: null);

        // Queue task to clear cache every 5 seconds for 3 times (mean that after 5,10,15s).
        // Delay because when save snippet text, fulltext index take amount of time to update, so that we wait
        // amount of time for fulltext index update
        // We also set executeOnceImmediately=true to clear cache immediately in case of some index is updated fast
        Util.TaskRunner.QueueIntervalAsyncActionInBackground(
            token => PlatformApplicationGlobal.CacheRepositoryProvider
                .GetCollection<TextSnippetCollectionCacheKeyProvider>()
                .RemoveAsync(cacheRequestKeyPartsPredicate: keyParts => keyParts[1] == removeFilterRequestCacheKeyParts[1], token),
            intervalTimeInSeconds: 5,
            CreateGlobalLogger,
            maximumIntervalExecutionCount: 3,
            executeOnceImmediately: true,
            cancellationToken);
    }
}
