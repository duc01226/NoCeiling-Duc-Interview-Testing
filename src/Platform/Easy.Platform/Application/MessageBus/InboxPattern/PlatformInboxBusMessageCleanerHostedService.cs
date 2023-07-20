using Easy.Platform.Application.Context;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxBusMessageCleanerHostedService : PlatformIntervalProcessHostedService
{
    public const int MinimumRetryCleanInboxMessageTimesToWarning = 3;

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

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => CleanInboxEventBusMessage(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanInboxMessageTimesToWarning)
                        Logger.LogWarning(
                            ex,
                            "Retry CleanInboxEventBusMessage {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                            currentRetry,
                            applicationSettingContext.ApplicationName,
                            applicationSettingContext.ApplicationAssembly.FullName);
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "CleanInboxEventBusMessage failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                applicationSettingContext.ApplicationName,
                applicationSettingContext.ApplicationAssembly.FullName);
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
        var totalProcessedMessages = await ServiceProvider.ExecuteScopedAsync(
            p => p.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                .CountAsync(p => p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed, cancellationToken));

        if (totalProcessedMessages > InboxConfig.MaxStoreProcessedMessageCount)
            await ProcessCleanMessageByMaxStoreProcessedMessageCount(totalProcessedMessages, cancellationToken);
        else
            await ProcessCleanMessageByExpiredTime(cancellationToken);
    }

    private async Task ProcessCleanMessageByMaxStoreProcessedMessageCount(int totalProcessedMessages, CancellationToken cancellationToken)
    {
        await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformInboxBusMessage>(
            async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
            {
                var toDeleteMessages = await inboxEventBusMessageRepo.GetAllAsync(
                    queryBuilder: query => query
                        .Where(p => p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed)
                        .OrderByDescending(p => p.LastConsumeDate)
                        .Skip(InboxConfig.MaxStoreProcessedMessageCount)
                        .Take(NumberOfDeleteMessagesBatch()),
                    cancellationToken);

                if (toDeleteMessages.Count > 0)
                    await inboxEventBusMessageRepo.DeleteManyAsync(
                        toDeleteMessages,
                        dismissSendEvent: true,
                        sendEntityEventConfigure: null,
                        cancellationToken);

                return toDeleteMessages;
            });

        Logger.LogInformation(
            "CleanInboxEventBusMessage success. Number of deleted messages: {DeletedMessagesCount}",
            totalProcessedMessages - InboxConfig.MaxStoreProcessedMessageCount);
    }

    private async Task ProcessCleanMessageByExpiredTime(CancellationToken cancellationToken)
    {
        var toDeleteMessageCount = await ServiceProvider.ExecuteScopedAsync(
            p => p.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                .CountAsync(
                    PlatformInboxBusMessage.ToCleanExpiredMessagesByTimeExpr(DeleteProcessedMessageInSeconds(), DeleteExpiredFailedMessageInSeconds()),
                    cancellationToken));

        if (toDeleteMessageCount > 0)
        {
            await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformInboxBusMessage>(
                async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    var expiredMessages = await inboxEventBusMessageRepo.GetAllAsync(
                        queryBuilder: query => query
                            .Where(
                                PlatformInboxBusMessage.ToCleanExpiredMessagesByTimeExpr(
                                    DeleteProcessedMessageInSeconds(),
                                    DeleteExpiredFailedMessageInSeconds()))
                            .OrderBy(p => p.LastConsumeDate)
                            .Take(NumberOfDeleteMessagesBatch()),
                        cancellationToken);

                    if (expiredMessages.Count > 0)
                        await inboxEventBusMessageRepo.DeleteManyAsync(
                            expiredMessages,
                            dismissSendEvent: true,
                            sendEntityEventConfigure: null,
                            cancellationToken);

                    return expiredMessages;
                });

            Logger.LogInformation("CleanInboxEventBusMessage success. Number of deleted messages: {DeletedMessageCount}", toDeleteMessageCount);
        }
    }
}
