using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

internal sealed class ClearCacheOnSaveSnippetTextCommandEventHandler : PlatformCqrsCommandEventApplicationHandler<SaveSnippetTextCommand, SaveSnippetTextCommandResult>
{
    private readonly IPlatformCacheRepositoryProvider cacheRepositoryProvider;

    public ClearCacheOnSaveSnippetTextCommandEventHandler(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(loggerFactory, unitOfWorkManager, serviceProvider, rootServiceProvider)
    {
        this.cacheRepositoryProvider = cacheRepositoryProvider;
    }

    // Default is true to improve performance when command save, the event is executed separately and could be in parallel.
    // Set it to false if you want the event executed sync with the command and in order
    // protected override bool MustWaitHandlerExecutionFinishedImmediately() => true;

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableInboxEventBusMessage => false;

    protected override async Task HandleAsync(
        PlatformCqrsCommandEvent<SaveSnippetTextCommand, SaveSnippetTextCommandResult> @event,
        CancellationToken cancellationToken)
    {
        Logger.Value.LogInformation("CommandResult : {CommandResult}", @event.CommandResult.ToFormattedJson());

        // Test slow event do not affect main command
        await Task.Delay(5.Seconds(), cancellationToken);

        var removeFilterRequestCacheKeyParts = SearchSnippetTextQuery.BuildCacheRequestKeyParts(request: null, userId: null, companyId: null);

        // Queue task to clear cache every 5 seconds for 3 times (mean that after 5,10,15s).
        // Delay because when save snippet text, fulltext index take amount of time to update, so that we wait
        // amount of time for fulltext index update
        // We also set executeOnceImmediately=true to clear cache immediately in case of some index is updated fast
        Util.TaskRunner.QueueIntervalAsyncActionInBackground(
            token => cacheRepositoryProvider
                .GetCollection<TextSnippetCollectionCacheKeyProvider>()
                .RemoveAsync(cacheRequestKeyPartsPredicate: keyParts => keyParts[1] == removeFilterRequestCacheKeyParts[1], token),
            intervalTimeInSeconds: 5,
            CreateGlobalLogger,
            maximumIntervalExecutionCount: 3,
            executeOnceImmediately: true,
            cancellationToken);
    }
}
