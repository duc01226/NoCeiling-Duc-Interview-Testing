using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.Utils;
using Easy.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxBusMessageCleanerHostedService : PlatformIntervalHostingBackgroundService
{
    public const int MinimumRetryCleanInboxMessageTimesToWarning = 3;
    public const int DefaultResilientDelayRetrySeconds = 10;
    public const int DefaultCleanDelayToNotStressSystemSeconds = 10;

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
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformPersistenceModule), Logger, $"process {GetType().Name}");

        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => CleanInboxEventBusMessage(cancellationToken),
                retryAttempt => DefaultResilientDelayRetrySeconds.Seconds(),
                retryCount: ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanInboxMessageTimesToWarning)
                        Logger.LogError(
                            "Retry CleanInboxEventBusMessage {CurrentRetry} time(s) failed: {Error}. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                            currentRetry,
                            ex.Message,
                            ApplicationSettingContext.ApplicationName,
                            ApplicationSettingContext.ApplicationAssembly.FullName);
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex.BeautifyStackTrace(),
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

    protected bool HasInboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);
    }

    protected async Task CleanInboxEventBusMessage(CancellationToken cancellationToken)
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
                p => p.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                    .CountAsync(p => p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed, cancellationToken));
            if (totalProcessedMessages <= InboxConfig.MaxStoreProcessedMessageCount) return;

            await ServiceProvider.ExecuteInjectScopedAsync(
                async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    await inboxEventBusMessageRepo.DeleteManyAsync(
                        queryBuilder: query => query
                            .Where(p => p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed)
                            .OrderByDescending(p => p.CreatedDate)
                            .Skip(InboxConfig.MaxStoreProcessedMessageCount),
                        dismissSendEvent: true,
                        cancellationToken: cancellationToken);
                });

            Logger.LogDebug(
                "ProcessCleanMessageByMaxStoreProcessedMessageCount success. Number of deleted messages: {DeletedMessagesCount}",
                totalProcessedMessages - InboxConfig.MaxStoreProcessedMessageCount);
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
            var toDeleteMessageCount = await ServiceProvider.ExecuteScopedAsync(
                p => p.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                    .CountAsync(
                        PlatformInboxBusMessage.ToCleanExpiredMessagesExpr(
                            InboxConfig.DeleteProcessedMessageInSeconds,
                            InboxConfig.DeleteExpiredIgnoredMessageInSeconds),
                        cancellationToken));

            if (toDeleteMessageCount > 0)
            {
                await ServiceProvider.ExecuteInjectScopedAsync(
                    async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                    {
                        var toDeleteMessageIds = await inboxEventBusMessageRepo.GetAllAsync(
                            queryBuilder: query => query
                                .Where(
                                    PlatformInboxBusMessage.ToCleanExpiredMessagesExpr(
                                        InboxConfig.DeleteProcessedMessageInSeconds,
                                        InboxConfig.DeleteExpiredIgnoredMessageInSeconds))
                                .Select(p => p.Id),
                            cancellationToken);

                        if (toDeleteMessageIds.Count > 0)
                            await inboxEventBusMessageRepo.DeleteManyAsync(
                                predicate: p => toDeleteMessageIds.Contains(p.Id),
                                dismissSendEvent: true,
                                eventCustomConfig: null,
                                cancellationToken);
                    });

                Logger.LogDebug("ProcessCleanMessageByExpiredTime success. Number of deleted messages: {DeletedMessageCount}", toDeleteMessageCount);
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
            var toIgnoreMessageCount = await ServiceProvider.ExecuteScopedAsync(
                p => p.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                    .CountAsync(
                        PlatformInboxBusMessage.ToIgnoreFailedExpiredMessagesExpr(
                            InboxConfig.IgnoreExpiredFailedMessageInSeconds,
                            InboxConfig.MaxRetriedProcessCount),
                        cancellationToken));

            if (toIgnoreMessageCount > 0)
            {
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () => await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformInboxBusMessage>(
                        maxExecutionCount: toIgnoreMessageCount / NumberOfDeleteMessagesBatch(),
                        async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                        {
                            var expiredMessages = await inboxEventBusMessageRepo.GetAllAsync(
                                queryBuilder: query => query
                                    .Where(
                                        PlatformInboxBusMessage.ToIgnoreFailedExpiredMessagesExpr(
                                            InboxConfig.IgnoreExpiredFailedMessageInSeconds,
                                            InboxConfig.MaxRetriedProcessCount))
                                    .OrderBy(p => p.CreatedDate)
                                    .Take(NumberOfDeleteMessagesBatch()),
                                cancellationToken);

                            if (expiredMessages.Count > 0)
                            {
                                await inboxEventBusMessageRepo.UpdateManyAsync(
                                    expiredMessages.SelectList(p => p.With(x => x.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Ignored)),
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

                Logger.LogDebug("ProcessIgnoreFailedMessageByExpiredTime success. Number of ignored messages: {DeletedMessageCount}", toIgnoreMessageCount);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.BeautifyStackTrace(), "ProcessIgnoreFailedMessageByExpiredTime failed");
        }
    }
}
