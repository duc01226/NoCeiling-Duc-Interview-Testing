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
    public const int DefaultResilientDelayRetrySeconds = 10;
    public const int DefaultCleanDelayToNotStressSystemSeconds = 10;

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
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformPersistenceModule), Logger, $"process {GetType().Name}");

        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => CleanOutboxEventBusMessage(cancellationToken),
                retryAttempt => DefaultResilientDelayRetrySeconds.Seconds(),
                retryCount: ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanOutboxMessageTimesToWarning)
                    {
                        Logger.LogError(
                            "Retry CleanOutboxEventBusMessage {CurrentRetry} time(s) failed: {Error}. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly}]",
                            currentRetry,
                            ex.Message,
                            ApplicationSettingContext.ApplicationName,
                            ApplicationSettingContext.ApplicationAssembly.FullName);
                    }
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex.BeautifyStackTrace(),
                "CleanOutboxEventBusMessage failed. [[Error:{Error}]] [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly}]",
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

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    protected async Task CleanOutboxEventBusMessage(CancellationToken cancellationToken)
    {
        await ProcessCleanMessageByMaxStoreProcessedMessageCount(cancellationToken);
        await ProcessCleanMessageByExpiredTime(cancellationToken);
        await ProcessIgnoreFailedMessageByExpiredTime(cancellationToken);
    }

    private async Task ProcessCleanMessageByMaxStoreProcessedMessageCount(CancellationToken cancellationToken)
    {
        try
        {
            var totalProcessedMessages = await ServiceProvider.ExecuteScopedAsync(
                p => p.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                    .CountAsync(p => p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed, cancellationToken));

            if (totalProcessedMessages <= OutboxConfig.MaxStoreProcessedMessageCount) return;

            await ServiceProvider.ExecuteInjectScopedAsync(
                async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                {
                    await outboxEventBusMessageRepo.DeleteManyAsync(
                        queryBuilder: query => query
                            .Where(p => p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed)
                            .OrderByDescending(p => p.CreatedDate)
                            .Skip(OutboxConfig.MaxStoreProcessedMessageCount),
                        dismissSendEvent: true,
                        cancellationToken: cancellationToken);
                });

            Logger.LogDebug(
                "ProcessCleanMessageByMaxStoreProcessedMessageCount success. Number of deleted messages: {DeletedMessageCount}",
                totalProcessedMessages - OutboxConfig.MaxStoreProcessedMessageCount);
        }
        catch (Exception e)
        {
            Logger.LogError(e.BeautifyStackTrace(), "ProcessCleanMessageByMaxStoreProcessedMessageCount failed");
        }
    }

    private async Task ProcessCleanMessageByExpiredTime(CancellationToken cancellationToken)
    {
        try
        {
            var toCleanMessageCount = await ServiceProvider.ExecuteScoped(
                scope => scope.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                    .CountAsync(
                        PlatformOutboxBusMessage.ToCleanExpiredMessagesExpr(
                            OutboxConfig.DeleteProcessedMessageInSeconds,
                            OutboxConfig.DeleteExpiredIgnoredMessageInSeconds),
                        cancellationToken));

            if (toCleanMessageCount > 0)
            {
                await ServiceProvider.ExecuteInjectScopedAsync(
                    async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                    {
                        await outboxEventBusMessageRepo.DeleteManyAsync(
                            PlatformOutboxBusMessage.ToCleanExpiredMessagesExpr(
                                OutboxConfig.DeleteProcessedMessageInSeconds,
                                OutboxConfig.DeleteExpiredIgnoredMessageInSeconds),
                            dismissSendEvent: true,
                            eventCustomConfig: null,
                            cancellationToken);
                    });

                Logger.LogDebug("ProcessCleanMessageByExpiredTime success. Number of deleted messages: {ToCleanMessageCount}", toCleanMessageCount);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.BeautifyStackTrace(), "ProcessCleanMessageByExpiredTime failed");
        }
    }

    private async Task ProcessIgnoreFailedMessageByExpiredTime(CancellationToken cancellationToken)
    {
        try
        {
            var toIgnoreMessageCount = await ServiceProvider.ExecuteScoped(
                scope => scope.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                    .CountAsync(
                        PlatformOutboxBusMessage.ToIgnoreFailedExpiredMessagesExpr(
                            OutboxConfig.IgnoreExpiredFailedMessageInSeconds),
                        cancellationToken));

            if (toIgnoreMessageCount > 0)
            {
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () => await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformOutboxBusMessage>(
                        maxExecutionCount: toIgnoreMessageCount / NumberOfDeleteMessagesBatch(),
                        async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                        {
                            var expiredMessages = await outboxEventBusMessageRepo.GetAllAsync(
                                queryBuilder: query =>
                                    query.Where(PlatformOutboxBusMessage.ToIgnoreFailedExpiredMessagesExpr(OutboxConfig.IgnoreExpiredFailedMessageInSeconds))
                                        .OrderBy(p => p.CreatedDate)
                                        .Take(NumberOfDeleteMessagesBatch()),
                                cancellationToken);

                            if (expiredMessages.Count > 0)
                            {
                                await outboxEventBusMessageRepo.UpdateManyAsync(
                                    expiredMessages.SelectList(p => p.With(x => x.SendStatus = PlatformOutboxBusMessage.SendStatuses.Ignored)),
                                    dismissSendEvent: true,
                                    eventCustomConfig: null,
                                    cancellationToken);

                                await Task.Delay(DefaultCleanDelayToNotStressSystemSeconds.Seconds(), cancellationToken);
                            }

                            return expiredMessages;
                        }),
                    _ => DefaultResilientDelayRetrySeconds.Seconds(),
                    ProcessClearMessageRetryCount(),
                    cancellationToken: cancellationToken);

                Logger.LogDebug("ProcessIgnoreFailedMessageByExpiredTime success. Number of ignored messages: {ToCleanMessageCount}", toIgnoreMessageCount);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.BeautifyStackTrace(), "ProcessIgnoreFailedMessageByExpiredTime failed");
        }
    }
}
