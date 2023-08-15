using Easy.Platform.Application;
using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;
using PlatformExampleApp.TextSnippet.Domain.Events;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

internal sealed class ClearCacheOnTransferSnippetTextToMultiDbDemoEntityNameDomainEvent
    : PlatformCqrsDomainEventApplicationHandler<TransferSnippetTextToMultiDbDemoEntityNameDomainEvent>
{
    public ClearCacheOnTransferSnippetTextToMultiDbDemoEntityNameDomainEvent(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider) : base(loggerFactory, unitOfWorkManager, serviceProvider)
    {
    }

    protected override async Task HandleAsync(TransferSnippetTextToMultiDbDemoEntityNameDomainEvent @event, CancellationToken cancellationToken)
    {
        // Test slow event do not affect main command
        await Task.Delay(5.Seconds(), cancellationToken);

        var removeFilterRequestCacheKeyParts = SearchSnippetTextQuery.BuildCacheRequestKeyParts(request: null, userId: null, companyId: null);

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
