using Easy.Platform.Application.Context;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxBusMessageCleanerHostedService : PlatformIntervalProcessHostedService
{
    public const int MinimumRetryCleanOutboxMessageTimesToWarning = 2;

    protected readonly PlatformOutboxConfig OutboxConfig;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;

    private bool isProcessing;

    public PlatformOutboxBusMessageCleanerHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        OutboxConfig = outboxConfig;
    }

    public static bool MatchImplementation(ServiceDescriptor serviceDescriptor)
    {
        return MatchImplementation(serviceDescriptor.ImplementationType) ||
               MatchImplementation(serviceDescriptor.ImplementationInstance?.GetType());
    }

    public static bool MatchImplementation(Type implementationType)
    {
        return implementationType?.IsAssignableTo(typeof(PlatformOutboxBusMessageCleanerHostedService)) == true;
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () => await CleanOutboxEventBusMessage(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanOutboxMessageTimesToWarning)
                        Logger.LogWarning(
                            ex,
                            $"Retry CleanOutboxEventBusMessage {currentRetry} time(s) failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                $"CleanOutboxEventBusMessage failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
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
        var toCleanMessageCount = await ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
            .CountAsync(
                PlatformOutboxBusMessage.ToCleanInboxEventBusMessagesExpr(DeleteProcessedMessageInSeconds(), DeleteExpiredFailedMessageInSeconds()),
                cancellationToken);

        if (toCleanMessageCount > 0)
        {
            await Util.Pager.ExecuteScrollingPagingAsync(
                async () =>
                {
                    return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformOutboxBusMessage>>(
                        async (IUnitOfWorkManager uowManager, IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                        {
                            using (var uow = uowManager!.Begin())
                            {
                                var expiredMessages = await outboxEventBusMessageRepo.GetAllAsync(
                                    queryBuilder: query => query
                                        .Where(
                                            PlatformOutboxBusMessage.ToCleanInboxEventBusMessagesExpr(
                                                DeleteProcessedMessageInSeconds(),
                                                DeleteExpiredFailedMessageInSeconds()))
                                        .OrderBy(p => p.LastSendDate)
                                        .Take(NumberOfDeleteMessagesBatch()),
                                    cancellationToken);

                                if (expiredMessages.Count > 0)
                                {
                                    await outboxEventBusMessageRepo.DeleteManyAsync(
                                        expiredMessages,
                                        dismissSendEvent: true,
                                        cancellationToken);

                                    await uow.CompleteAsync(cancellationToken);
                                }

                                return expiredMessages;
                            }
                        });
                });

            Logger.LogInformation(
                message:
                $"CleanOutboxEventBusMessage success. Number of deleted messages: {toCleanMessageCount}");
        }
    }
}
