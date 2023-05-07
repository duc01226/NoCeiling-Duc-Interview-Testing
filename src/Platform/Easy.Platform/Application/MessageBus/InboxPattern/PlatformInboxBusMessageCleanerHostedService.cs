using Easy.Platform.Application.Context;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxBusMessageCleanerHostedService : PlatformIntervalProcessHostedService
{
    public const int MinimumRetryCleanInboxMessageTimesToWarning = 2;

    protected readonly PlatformInboxConfig InboxConfig;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;

    private bool isProcessing;

    public PlatformInboxBusMessageCleanerHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformInboxConfig inboxConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        InboxConfig = inboxConfig;
    }

    public static bool MatchImplementation(ServiceDescriptor serviceDescriptor)
    {
        return MatchImplementation(serviceDescriptor.ImplementationType) ||
               MatchImplementation(serviceDescriptor.ImplementationInstance?.GetType());
    }

    public static bool MatchImplementation(Type implementationType)
    {
        return implementationType?.IsAssignableTo(typeof(PlatformInboxBusMessageCleanerHostedService)) == true;
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () => await CleanInboxEventBusMessage(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanInboxMessageTimesToWarning)
                        Logger.LogWarning(
                            ex,
                            $"Retry CleanInboxEventBusMessage {currentRetry} time(s) failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                $"CleanInboxEventBusMessage failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
        }

        isProcessing = false;
    }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return InboxConfig.MessageCleanerTriggerIntervalInMinutes.Minutes();
    }

    protected virtual int ProcessClearMessageRetryCount()
    {
        return InboxConfig.ProcessClearMessageRetryCount;
    }

    /// <inheritdoc cref="PlatformInboxConfig.NumberOfDeleteMessagesBatch" />
    protected virtual int NumberOfDeleteMessagesBatch()
    {
        return InboxConfig.NumberOfDeleteMessagesBatch;
    }

    /// <inheritdoc cref="PlatformInboxConfig.DeleteProcessedMessageInSeconds" />
    protected virtual double DeleteProcessedMessageInSeconds()
    {
        return InboxConfig.DeleteProcessedMessageInSeconds;
    }

    /// <inheritdoc cref="PlatformInboxConfig.DeleteExpiredFailedMessageInSeconds" />
    protected virtual double DeleteExpiredFailedMessageInSeconds()
    {
        return InboxConfig.DeleteExpiredFailedMessageInSeconds;
    }

    protected bool HasInboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);
    }

    protected async Task CleanInboxEventBusMessage(CancellationToken cancellationToken)
    {
        var toCleanMessageCount = await ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
            .CountAsync(
                PlatformInboxBusMessage.ToCleanInboxEventBusMessagesExpr(DeleteProcessedMessageInSeconds(), DeleteExpiredFailedMessageInSeconds()),
                cancellationToken);

        if (toCleanMessageCount > 0)
        {
            await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformInboxBusMessage>(
                async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    using (var uow = uowManager!.Begin())
                    {
                        var expiredMessages = await inboxEventBusMessageRepo.GetAllAsync(
                            queryBuilder: query => query
                                .Where(
                                    PlatformInboxBusMessage.ToCleanInboxEventBusMessagesExpr(
                                        DeleteProcessedMessageInSeconds(),
                                        DeleteExpiredFailedMessageInSeconds()))
                                .OrderBy(p => p.LastConsumeDate)
                                .Take(NumberOfDeleteMessagesBatch()),
                            cancellationToken);

                        if (expiredMessages.Count > 0)
                        {
                            await inboxEventBusMessageRepo.DeleteManyAsync(
                                expiredMessages,
                                dismissSendEvent: true,
                                cancellationToken);

                            await uow.CompleteAsync(cancellationToken);
                        }

                        return expiredMessages;
                    }
                });

            Logger.LogInformation(
                message:
                $"CleanInboxEventBusMessage success. Number of deleted messages: {toCleanMessageCount}");
        }
    }
}
