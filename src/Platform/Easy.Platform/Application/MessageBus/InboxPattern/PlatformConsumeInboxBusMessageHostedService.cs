using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Timing;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

/// <summary>
/// Run in an interval to scan messages in InboxCollection in database, check new message to consume it
/// </summary>
public class PlatformConsumeInboxBusMessageHostedService : PlatformIntervalHostingBackgroundService
{
    public const int MinimumRetryConsumeInboxMessageTimesToWarning = 3;

    private bool isProcessing;

    public PlatformConsumeInboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformInboxConfig inboxConfig,
        PlatformMessageBusConfig messageBusConfig) : base(serviceProvider, loggerFactory)
    {
        ApplicationSettingContext = applicationSettingContext;
        InboxConfig = inboxConfig;
        MessageBusConfig = messageBusConfig;
        ConsumerByNameToTypeDic = messageBusScanner
            .ScanAllDefinedConsumerTypes()
            .ToDictionary(PlatformInboxBusMessage.GetConsumerByValue);
        InvokeConsumerLogger = loggerFactory.CreateLogger(typeof(PlatformMessageBusConsumer));
    }

    public override bool LogIntervalProcessInformation => InboxConfig.LogIntervalProcessInformation;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }
    protected PlatformInboxConfig InboxConfig { get; }
    protected PlatformMessageBusConfig MessageBusConfig { get; }
    protected Dictionary<string, Type> ConsumerByNameToTypeDic { get; }
    protected ILogger InvokeConsumerLogger { get; }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformModule), Logger, $"process ${GetType().Name}");

        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing)
            return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the database is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => ConsumeInboxEventBusMessages(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: InboxConfig.ProcessConsumeMessageRetryCount,
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= MinimumRetryConsumeInboxMessageTimesToWarning)
                        Logger.LogError(
                            ex,
                            "Retry ConsumeInboxEventBusMessages {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
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
                "Retry ConsumeInboxEventBusMessages failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                ApplicationSettingContext.ApplicationName,
                ApplicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected virtual async Task ConsumeInboxEventBusMessages(CancellationToken cancellationToken)
    {
        do
        {
            var toHandleMessages = await PopToHandleInboxEventBusMessages(cancellationToken);

            // Group by ConsumerBy to handling multiple different consumers parallel
            await toHandleMessages
                .GroupBy(p => p.ConsumerBy)
                .ParallelAsync(
                    async consumerMessages =>
                    {
                        // Message in the same consumer queue but created on the same seconds usually from different data/users and not dependent,
                        // so that we could process it in parallel
                        await consumerMessages
                            .GroupBy(p => p.CreatedDate.AddMilliseconds(-p.CreatedDate.Millisecond))
                            .ForEachAsync(groupSameTimeSeconds => groupSameTimeSeconds.ParallelAsync(HandleInboxMessageAsync));
                    });

            // Random wait to decrease the chance that multiple deploy instance could process same messages at the same time
            await Task.Delay(Util.RandomGenerator.Next(5, 10).Seconds(), cancellationToken);
        } while (await IsAnyMessagesToHandleAsync());

        async Task HandleInboxMessageAsync(PlatformInboxBusMessage toHandleInboxMessage)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                try
                {
                    await InvokeConsumerAsync(
                        scope,
                        toHandleInboxMessage,
                        cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        e,
                        "[PlatformConsumeInboxEventBusMessageHostedService] Try to consume inbox message with Id:{MessageId} failed. Message Content:{InboxMessage}",
                        toHandleInboxMessage.Id,
                        toHandleInboxMessage.ToJson());
                }
            }
        }
    }

    protected Task<bool> IsAnyMessagesToHandleAsync()
    {
        return ServiceProvider.ExecuteInjectScopedAsync<bool>(
            (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
            {
                return inboxEventBusMessageRepo!.AnyAsync(
                    PlatformInboxBusMessage.CanHandleMessagesExpr(InboxConfig.MessageProcessingMaxSeconds));
            });
    }

    protected virtual async Task InvokeConsumerAsync(
        IServiceScope scope,
        PlatformInboxBusMessage toHandleInboxMessage,
        CancellationToken cancellationToken)
    {
        var consumerType = ResolveConsumerType(toHandleInboxMessage);

        if (consumerType != null)
        {
            var consumer = scope.ServiceProvider.GetService(consumerType)
                .As<IPlatformApplicationMessageBusConsumer>()
                .With(_ => _.HandleExistingInboxMessage = toHandleInboxMessage);

            var consumerMessageType = PlatformMessageBusConsumer.GetConsumerMessageType(consumer);

            var busMessage = Util.TaskRunner.CatchExceptionContinueThrow(
                () => PlatformJsonSerializer.Deserialize(
                    toHandleInboxMessage.JsonMessage,
                    consumerMessageType,
                    consumer.CustomJsonSerializerOptions()),
                ex => Logger.LogError(
                    ex,
                    "RabbitMQ parsing message to {ConsumerMessageType.Name}. [[Error:{Error}]]. Body: {InboxMessage}",
                    consumerMessageType.Name,
                    ex.Message,
                    toHandleInboxMessage.JsonMessage));

            if (busMessage != null)
                await PlatformMessageBusConsumer.InvokeConsumerAsync(
                    consumer,
                    busMessage,
                    toHandleInboxMessage.RoutingKey,
                    MessageBusConfig,
                    InvokeConsumerLogger);
        }
        else
        {
            Logger.LogWarning(
                "[{LoggerType}] Error resolve consumer type {ToHandleInboxMessageConsumerBy}. InboxId:{ToHandleInboxMessageId}",
                GetType().Name,
                toHandleInboxMessage.ConsumerBy,
                toHandleInboxMessage.Id);
        }
    }

    protected async Task<List<PlatformInboxBusMessage>> PopToHandleInboxEventBusMessages(
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformInboxBusMessage>>(
                async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    var toHandleMessages = await inboxEventBusMessageRepo.GetAllAsync(
                        queryBuilder: query => query
                            .Where(PlatformInboxBusMessage.CanHandleMessagesExpr(InboxConfig.MessageProcessingMaxSeconds))
                            .OrderBy(p => p.LastConsumeDate)
                            .Take(InboxConfig.NumberOfProcessConsumeInboxMessagesBatch),
                        cancellationToken);

                    if (toHandleMessages.IsEmpty()) return toHandleMessages;

                    toHandleMessages.ForEach(
                        p =>
                        {
                            p.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processing;
                            p.LastConsumeDate = Clock.UtcNow;
                        });

                    await inboxEventBusMessageRepo.UpdateManyAsync(
                        toHandleMessages,
                        dismissSendEvent: true,
                        eventCustomConfig: null,
                        cancellationToken);

                    return toHandleMessages;
                });
        }
        catch (PlatformDomainRowVersionConflictException conflictDomainException)
        {
            Logger.LogDebug(
                conflictDomainException,
                "Some other consumer instance has been handling some inbox messages (support multi service instance running concurrently), which lead to row version conflict. This is as expected.");

            // WHY: Because support multiple service instance running concurrently,
            // get row version conflict is expected, so just retry again to get unprocessed inbox messages
            return await PopToHandleInboxEventBusMessages(cancellationToken);
        }
    }

    protected bool HasInboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);
    }

    private Type ResolveConsumerType(PlatformInboxBusMessage toHandleInboxMessage)
    {
        return ConsumerByNameToTypeDic.GetValueOrDefault(toHandleInboxMessage.ConsumerBy, null);
    }
}
