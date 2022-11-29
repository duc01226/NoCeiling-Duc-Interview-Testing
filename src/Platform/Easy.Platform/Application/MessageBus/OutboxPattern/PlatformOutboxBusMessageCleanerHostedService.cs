using Easy.Platform.Application.Context;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Easy.Platform.Common.Timing;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxBusMessageCleanerHostedService : PlatformIntervalProcessHostedService
{
    /// <summary>
    /// Default number messages is deleted in every process. Default is 10;
    /// </summary>
    public const int DefaultNumberOfDeleteMessagesBatch = 10;

    public const int MinimumRetryCleanOutboxMessageTimesToWarning = 2;

    public const string DefaultDeleteProcessedMessageInSecondsSettingKey = "MessageBus:OutboxDeleteProcessedMessageInSeconds";

    protected readonly IConfiguration Configuration;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;

    private bool isProcessing;

    public PlatformOutboxBusMessageCleanerHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        IConfiguration configuration) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        Configuration = configuration;
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
                () => CleanOutboxEventBusMessage(cancellationToken),
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

    protected virtual int ProcessClearMessageRetryCount()
    {
        return 5;
    }

    /// <summary>
    /// To config maximum number messages is deleted in every process. Default is
    /// <see cref="DefaultNumberOfDeleteMessagesBatch" />;
    /// </summary>
    protected virtual int NumberOfDeleteMessagesBatch()
    {
        return DefaultNumberOfDeleteMessagesBatch;
    }

    /// <summary>
    /// To config how long a message can live in the database in seconds. Default is one week (7 day);
    /// </summary>
    protected virtual double DeleteProcessedMessageInSeconds()
    {
        return Configuration.GetSection(DefaultDeleteProcessedMessageInSecondsSettingKey)?.Get<int?>() ??
               7.Days().TotalSeconds;
    }

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    protected async Task CleanOutboxEventBusMessage(CancellationToken cancellationToken)
    {
        await ServiceProvider.ExecuteInjectScopedAsync(
            async (IUnitOfWorkManager uowManager, IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
            {
                using (var uow = uowManager!.Begin())
                {
                    var expiredMessages = await outboxEventBusMessageRepo.GetAllAsync(
                        queryBuilder: query => query.Where(
                                p => p.LastSendDate <= Clock.UtcNow.AddSeconds(-DeleteProcessedMessageInSeconds()) &&
                                     p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed)
                            .OrderBy(p => p.SendStatus)
                            .Take(NumberOfDeleteMessagesBatch()),
                        cancellationToken);

                    if (expiredMessages.Count > 0)
                    {
                        await outboxEventBusMessageRepo.DeleteManyAsync(
                            expiredMessages,
                            dismissSendEvent: true,
                            cancellationToken);

                        await uow.CompleteAsync(cancellationToken);

                        Logger.LogInformation(
                            message:
                            $"CleanOutboxEventBusMessage success. Number of deleted messages: {expiredMessages.Count}");
                    }
                }
            });
    }
}
