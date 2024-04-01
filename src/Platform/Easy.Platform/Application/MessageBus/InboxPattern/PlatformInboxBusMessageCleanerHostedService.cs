using System.Linq.Expressions;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxBusMessageCleanerHostedService : PlatformIntervalHostingBackgroundService
{
    public const int MinimumRetryCleanInboxMessageTimesToWarning = 3;

    private bool isProcessing;

    public PlatformInboxBusMessageCleanerHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformInboxConfig inboxConfig) : base(serviceProvider, loggerFactory)
    {
        ApplicationSettingContext = applicationSettingContext;
        InboxConfig = inboxConfig;
    }

    public override bool LogIntervalProcessInformation => InboxConfig.LogIntervalProcessInformation;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    protected PlatformInboxConfig InboxConfig { get; }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformModule), Logger, $"process ${GetType().Name}");

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
                        Logger.LogError(
                            ex,
                            "Retry CleanInboxEventBusMessage {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                            currentRetry,
                            ApplicationSettingContext.ApplicationName,
                            ApplicationSettingContext.ApplicationAssembly.FullName);
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "CleanInboxEventBusMessage failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                ApplicationSettingContext.ApplicationName,
                ApplicationSettingContext.ApplicationAssembly.FullName);
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
            maxExecutionCount: await ServiceProvider.ExecuteScopedAsync(
                p => p.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                    .CountAsync(CleanMessagePredicate(), cancellationToken: cancellationToken)
                    .Then(total => total / NumberOfDeleteMessagesBatch())),
            async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
            {
                var toDeleteMessages = await inboxEventBusMessageRepo.GetAllAsync(
                    queryBuilder: query => query
                        .Where(CleanMessagePredicate())
                        .OrderByDescending(p => p.LastConsumeDate)
                        .Skip(InboxConfig.MaxStoreProcessedMessageCount)
                        .Take(NumberOfDeleteMessagesBatch()),
                    cancellationToken);

                if (toDeleteMessages.Count > 0)
                    await inboxEventBusMessageRepo.DeleteManyAsync(
                        toDeleteMessages,
                        dismissSendEvent: true,
                        eventCustomConfig: null,
                        cancellationToken);

                return toDeleteMessages;
            });

        Logger.LogInformation(
            "CleanInboxEventBusMessage success. Number of deleted messages: {DeletedMessagesCount}",
            totalProcessedMessages - InboxConfig.MaxStoreProcessedMessageCount);

        static Expression<Func<PlatformInboxBusMessage, bool>> CleanMessagePredicate()
        {
            return p => p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed;
        }
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
                maxExecutionCount: toDeleteMessageCount / NumberOfDeleteMessagesBatch(),
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
                            eventCustomConfig: null,
                            cancellationToken);

                    return expiredMessages;
                });

            Logger.LogInformation("CleanInboxEventBusMessage success. Number of deleted messages: {DeletedMessageCount}", toDeleteMessageCount);
        }
    }
}
