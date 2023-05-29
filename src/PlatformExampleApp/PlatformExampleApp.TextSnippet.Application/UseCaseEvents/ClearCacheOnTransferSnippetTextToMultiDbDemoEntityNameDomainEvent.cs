using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Domain.Events;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

internal sealed class ClearCacheOnTransferSnippetTextToMultiDbDemoEntityNameDomainEvent
    : PlatformCqrsDomainEventApplicationHandler<TransferSnippetTextToMultiDbDemoEntityNameDomainEvent>
{
    private readonly IPlatformCacheRepositoryProvider cacheRepositoryProvider;

    public ClearCacheOnTransferSnippetTextToMultiDbDemoEntityNameDomainEvent(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(loggerFactory, unitOfWorkManager, serviceProvider)
    {
        this.cacheRepositoryProvider = cacheRepositoryProvider;
    }

    protected override async Task HandleAsync(TransferSnippetTextToMultiDbDemoEntityNameDomainEvent @event, CancellationToken cancellationToken)
    {
        // Test slow event do not affect main command
        await Task.Delay(5.Seconds(), cancellationToken);

        Util.TaskRunner.QueueIntervalAsyncActionInBackground(
            token => cacheRepositoryProvider.Get()
                .RemoveCollectionAsync<TextSnippetCollectionCacheKeyProvider>(token),
            intervalTimeInSeconds: 5,
            CreateGlobalLogger,
            maximumIntervalExecutionCount: 3,
            executeOnceImmediately: true,
            cancellationToken);
    }
}
