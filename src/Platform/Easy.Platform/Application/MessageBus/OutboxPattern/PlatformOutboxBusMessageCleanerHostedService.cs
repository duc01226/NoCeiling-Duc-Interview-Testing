using System.Linq.Expressions;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.Utils;
using Easy.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxBusMessageCleanerHostedService : PlatformIntervalHostingBackgroundService
{
    public const int MinimumRetryCleanOutboxMessageTimesToWarning = 3;

    private bool isProcessing;

    public PlatformOutboxBusMessageCleanerHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(serviceProvider, loggerFactory)
    {
        ApplicationSettingContext = applicationSettingContext;
        OutboxConfig = outboxConfig;
    }

    public override bool LogIntervalProcessInformation => OutboxConfig.LogIntervalProcessInformation;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    protected PlatformOutboxConfig OutboxConfig { get; }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformPersistenceModule), Logger, $"process ${GetType().Name}");

        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => CleanOutboxEventBusMessage(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanOutboxMessageTimesToWarning)
                        Logger.LogError(
                            ex,
                            "Retry CleanOutboxEventBusMessage {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
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
                "CleanOutboxEventBusMessage failed. [[Error:{Error}]] [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                ex.Message,
                ApplicationSettingContext.ApplicationName,
                ApplicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return OutboxConfig.MessageCleanerTriggerIntervalInMinutes.Minutes();
    }

    protected virtual int ProcessClearMessageRetryCount()
    {
        return OutboxConfig.ProcessClearMessageRetryCount;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.NumberOfDeleteMessagesBatch" />
    protected virtual int NumberOfDeleteMessagesBatch()
    {
        return OutboxConfig.NumberOfDeleteMessagesBatch;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.DeleteProcessedMessageInSeconds" />
    protected virtual double DeleteProcessedMessageInSeconds()
    {
        return OutboxConfig.DeleteProcessedMessageInSeconds;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.DeleteExpiredFailedMessageInSeconds" />
    protected virtual double DeleteExpiredFailedMessageInSeconds()
    {
        return OutboxConfig.DeleteExpiredFailedMessageInSeconds;
    }

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    protected async Task CleanOutboxEventBusMessage(CancellationToken cancellationToken)
    {
        var totalProcessedMessages = await ServiceProvider.ExecuteScopedAsync(
            p => p.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                .CountAsync(p => p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed, cancellationToken));

        if (totalProcessedMessages > OutboxConfig.MaxStoreProcessedMessageCount)
            await ProcessCleanMessageByMaxStoreProcessedMessageCount(totalProcessedMessages, cancellationToken);
        else
            await ProcessCleanMessageByExpiredTime(cancellationToken);
    }

    private async Task ProcessCleanMessageByMaxStoreProcessedMessageCount(int totalProcessedMessages, CancellationToken cancellationToken)
    {
        await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformOutboxBusMessage>(
            maxExecutionCount: await ServiceProvider.ExecuteScopedAsync(
                p => p.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                    .CountAsync(CleanMessagePredicate(), cancellationToken: cancellationToken)
                    .Then(total => total / NumberOfDeleteMessagesBatch())),
            async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
            {
                var toDeleteMessages = await outboxEventBusMessageRepo.GetAllAsync(
                    queryBuilder: query => query
                        .Where(CleanMessagePredicate())
                        .OrderByDescending(p => p.CreatedDate)
                        .Skip(OutboxConfig.MaxStoreProcessedMessageCount)
                        .Take(NumberOfDeleteMessagesBatch()),
                    cancellationToken);

                if (toDeleteMessages.Count > 0)
                    await outboxEventBusMessageRepo.DeleteManyAsync(
                        toDeleteMessages,
                        dismissSendEvent: true,
                        eventCustomConfig: null,
                        cancellationToken);

                return toDeleteMessages;
            });

        Logger.LogInformation(
            "CleanOutboxEventBusMessage success. Number of deleted messages: {DeletedMessageCount}",
            totalProcessedMessages - OutboxConfig.MaxStoreProcessedMessageCount);

        static Expression<Func<PlatformOutboxBusMessage, bool>> CleanMessagePredicate()
        {
            return p => p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed;
        }
    }

    private async Task ProcessCleanMessageByExpiredTime(CancellationToken cancellationToken)
    {
        var toCleanMessageCount = await ServiceProvider.ExecuteScoped(
            scope => scope.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                .CountAsync(
                    PlatformOutboxBusMessage.ToCleanExpiredMessagesByTimeExpr(DeleteProcessedMessageInSeconds(), DeleteExpiredFailedMessageInSeconds()),
                    cancellationToken));

        if (toCleanMessageCount > 0)
        {
            await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformOutboxBusMessage>(
                maxExecutionCount: toCleanMessageCount / NumberOfDeleteMessagesBatch(),
                async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                {
                    var expiredMessages = await outboxEventBusMessageRepo.GetAllAsync(
                        queryBuilder: query => query
                            .Where(
                                PlatformOutboxBusMessage.ToCleanExpiredMessagesByTimeExpr(
                                    DeleteProcessedMessageInSeconds(),
                                    DeleteExpiredFailedMessageInSeconds()))
                            .OrderBy(p => p.CreatedDate)
                            .Take(NumberOfDeleteMessagesBatch()),
                        cancellationToken);

                    if (expiredMessages.Count > 0)
                        await outboxEventBusMessageRepo.DeleteManyAsync(
                            expiredMessages,
                            dismissSendEvent: true,
                            eventCustomConfig: null,
                            cancellationToken);

                    return expiredMessages;
                });

            Logger.LogInformation("CleanOutboxEventBusMessage success. Number of deleted messages: {ToCleanMessageCount}", toCleanMessageCount);
        }
    }
}
