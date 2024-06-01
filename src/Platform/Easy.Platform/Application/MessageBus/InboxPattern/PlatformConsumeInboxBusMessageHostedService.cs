using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Timing;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Infrastructures.MessageBus;
using Easy.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

/// <summary>
/// Run in an interval to scan messages in InboxCollection in database, check new message to consume it
/// </summary>
public class PlatformConsumeInboxBusMessageHostedService : PlatformIntervalHostingBackgroundService
{
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
        AvailableConsumerByNameToTypeDic = messageBusScanner
            .ScanAllDefinedConsumerTypes()
            .ToDictionary(PlatformInboxBusMessage.GetConsumerByValue);
        InvokeConsumerLogger = loggerFactory.CreateLogger(typeof(PlatformMessageBusConsumer).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}");
    }

    public override bool LogIntervalProcessInformation => InboxConfig.LogIntervalProcessInformation;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }
    protected PlatformInboxConfig InboxConfig { get; }
    protected PlatformMessageBusConfig MessageBusConfig { get; }
    protected Dictionary<string, Type> AvailableConsumerByNameToTypeDic { get; }
    protected ILogger InvokeConsumerLogger { get; }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return InboxConfig.CheckToProcessTriggerIntervalTimeSeconds.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformPersistenceModule), Logger, $"process {GetType().Name}");

        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing) return;

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
                    if (currentRetry >= InboxConfig.MinimumRetryConsumeInboxMessageTimesToWarning)
                        Logger.LogError(
                            ex.BeautifyStackTrace(),
                            "Retry ConsumeInboxEventBusMessages {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly}]",
                            currentRetry,
                            ApplicationSettingContext.ApplicationName,
                            ApplicationSettingContext.ApplicationAssembly.FullName);
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex.BeautifyStackTrace(),
                "Retry ConsumeInboxEventBusMessages failed. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly}]",
                ApplicationSettingContext.ApplicationName,
                ApplicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected virtual async Task ConsumeInboxEventBusMessages(CancellationToken cancellationToken)
    {
        await ServiceProvider.ExecuteInjectScopedAsync(
            async (IPlatformInboxBusMessageRepository inboxBusMessageRepository) =>
            {
                do
                {
                    try
                    {
                        // Random delay to prevent chance multiple api pod instance scan at the same time
                        await Task.Delay(millisecondsDelay: Random.Shared.Next(1, 10) * 1000, cancellationToken: cancellationToken);

                        var processedCanHandleMessageGroupedByConsumerIdPrefixes = new HashSet<string>();

                        await Util.Pager.ExecutePagingAsync(
                            async (skipCount, pageSize) =>
                            {
                                var pagedCanHandleMessageGroupedByConsumerIdPrefixes = await inboxBusMessageRepository.GetAllAsync(
                                        queryBuilder: query => query
                                            .Where(
                                                PlatformInboxBusMessage.CanHandleMessagesExpr(
                                                    InboxConfig.MessageProcessingMaxSeconds,
                                                    ApplicationSettingContext.ApplicationName))
                                            .Skip(skipCount)
                                            .Take(pageSize)
                                            .Select(p => p.Id),
                                        cancellationToken: cancellationToken)
                                    .Then(
                                        messageIds => messageIds
                                            .Select(PlatformInboxBusMessage.GetIdPrefix)
                                            .Where(p => !processedCanHandleMessageGroupedByConsumerIdPrefixes.Contains(p))
                                            .ToList());

                                await pagedCanHandleMessageGroupedByConsumerIdPrefixes.ParallelAsync(
                                    async messageGroupedByConsumerIdPrefix =>
                                    {
                                        do
                                        {
                                            try
                                            {
                                                var handlePreviousMessageFailed = false;

                                                var toHandleMessages = await PopToHandleInboxEventBusMessages(
                                                    messageGroupedByConsumerIdPrefix,
                                                    pageSize: InboxConfig.NumberOfProcessConsumeInboxMessagesSubQueuePrefetch,
                                                    cancellationToken);

                                                if (toHandleMessages.IsEmpty()) break;

                                                foreach (var toHandleInboxMessage in toHandleMessages)
                                                    try
                                                    {
                                                        if (handlePreviousMessageFailed)
                                                            await PlatformInboxMessageBusConsumerHelper.RevertExistingInboxToNewMessageAsync(
                                                                toHandleInboxMessage,
                                                                inboxBusMessageRepository,
                                                                cancellationToken);
                                                        else await HandleInboxMessageAsync(toHandleInboxMessage);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        handlePreviousMessageFailed = true;
                                                        Logger.LogError(
                                                            e.BeautifyStackTrace(),
                                                            "[PlatformConsumeInboxEventBusMessageHostedService] Try to consume inbox message with Id:{MessageId} failed. Message Content:{InboxMessage}",
                                                            toHandleInboxMessage.Id,
                                                            toHandleInboxMessage.ToFormattedJson());
                                                    }

                                                if (handlePreviousMessageFailed) break;
                                            }
                                            finally
                                            {
                                                Util.GarbageCollector.Collect();
                                            }
                                        } while (true);
                                    },
                                    InboxConfig.NumberOfProcessConsumeInboxParallelMessages);

                                pagedCanHandleMessageGroupedByConsumerIdPrefixes.ForEach(p => processedCanHandleMessageGroupedByConsumerIdPrefixes.Add(p));
                            },
                            maxItemCount: await inboxBusMessageRepository.CountAsync(
                                queryBuilder: query => query
                                    .Where(
                                        PlatformInboxBusMessage.CanHandleMessagesExpr(
                                            InboxConfig.MessageProcessingMaxSeconds,
                                            ApplicationSettingContext.ApplicationName)),
                                cancellationToken: cancellationToken),
                            pageSize: InboxConfig.GetCanHandleMessageGroupedByConsumerIdPrefixesPageSize,
                            cancellationToken: cancellationToken);
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                } while (await AnyCanHandleInboxBusMessages(null, inboxBusMessageRepository));
            });

        async Task HandleInboxMessageAsync(PlatformInboxBusMessage toHandleInboxMessage)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                await InvokeConsumerAsync(
                    scope,
                    toHandleInboxMessage,
                    cancellationToken);
            }
        }
    }

    protected async Task<bool> AnyCanHandleInboxBusMessages(
        string messageGroupedByConsumerIdPrefix,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository)
    {
        var toHandleMessages = await inboxBusMessageRepository.GetAllAsync(
            queryBuilder: query => CanHandleMessagesByConsumerIdPrefixQueryBuilder(query, messageGroupedByConsumerIdPrefix).Take(1));

        var result = toHandleMessages.Any() &&
                     !await inboxBusMessageRepository.AnyAsync(
                         PlatformInboxBusMessage.CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(toHandleMessages.First()));

        return result;
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
                .With(p => p.HandleExistingInboxMessage = toHandleInboxMessage)
                .With(p => p.NeedToCheckAnySameConsumerOtherPreviousNotProcessedInboxMessage = false);

            var consumerMessageType = PlatformMessageBusConsumer.GetConsumerMessageType(consumer);

            var busMessage = Util.TaskRunner.CatchExceptionContinueThrow(
                () => PlatformJsonSerializer.Deserialize(
                    toHandleInboxMessage.JsonMessage,
                    consumerMessageType,
                    consumer.CustomJsonSerializerOptions()),
                ex => Logger.LogError(
                    ex.BeautifyStackTrace(),
                    "RabbitMQ parsing message to {ConsumerMessageType}. [[Error:{Error}]]. Body: {InboxMessage}",
                    consumerMessageType.Name,
                    ex.Message,
                    toHandleInboxMessage.JsonMessage));

            if (busMessage != null)
                try
                {
                    if (consumer.HandleWhen(busMessage, toHandleInboxMessage.RoutingKey))
                        await PlatformMessageBusConsumer.InvokeConsumerAsync(
                            consumer,
                            busMessage,
                            toHandleInboxMessage.RoutingKey,
                            MessageBusConfig,
                            InvokeConsumerLogger);
                    else
                        await scope.ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>()
                            .DeleteImmediatelyAsync(toHandleInboxMessage.Id, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    await PlatformInboxMessageBusConsumerHelper.UpdateExistingInboxFailedMessageAsync(
                        ServiceProvider,
                        toHandleInboxMessage,
                        toHandleInboxMessage.JsonMessage.JsonDeserialize<object>(),
                        consumer.GetType(),
                        toHandleInboxMessage.RoutingKey,
                        ex,
                        PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                        () => Logger,
                        cancellationToken);
                }
        }
        else
        {
            await PlatformInboxMessageBusConsumerHelper.UpdateExistingInboxFailedMessageAsync(
                ServiceProvider,
                toHandleInboxMessage,
                toHandleInboxMessage.JsonMessage.JsonDeserialize<object>(),
                null,
                toHandleInboxMessage.RoutingKey,
                new Exception($"Error resolve consumer type {toHandleInboxMessage.ConsumerBy}. InboxId:{toHandleInboxMessage.Id}"),
                PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                () => Logger,
                cancellationToken);
        }
    }

    protected async Task<List<PlatformInboxBusMessage>> PopToHandleInboxEventBusMessages(
        string messageGroupedByConsumerIdPrefix,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformInboxBusMessage>>(
                async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    if (!await AnyCanHandleInboxBusMessages(messageGroupedByConsumerIdPrefix, inboxEventBusMessageRepo)) return [];

                    var toHandleMessages = await inboxEventBusMessageRepo.GetAllAsync(
                        queryBuilder: query => CanHandleMessagesByConsumerIdPrefixQueryBuilder(query, messageGroupedByConsumerIdPrefix).Take(pageSize),
                        cancellationToken);

                    if (toHandleMessages.IsEmpty() ||
                        await inboxEventBusMessageRepo.AnyAsync(
                            PlatformInboxBusMessage.CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(toHandleMessages.First()),
                            cancellationToken)) return [];

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
            return await PopToHandleInboxEventBusMessages(messageGroupedByConsumerIdPrefix, pageSize, cancellationToken);
        }
    }

    protected IQueryable<PlatformInboxBusMessage> CanHandleMessagesByConsumerIdPrefixQueryBuilder(
        IQueryable<PlatformInboxBusMessage> query,
        string messageGroupedByConsumerIdPrefix)
    {
        return query
            .WhereIf(messageGroupedByConsumerIdPrefix.IsNotNullOrEmpty(), p => p.Id.StartsWith(messageGroupedByConsumerIdPrefix))
            .Where(PlatformInboxBusMessage.CanHandleMessagesExpr(InboxConfig.MessageProcessingMaxSeconds, ApplicationSettingContext.ApplicationName))
            .OrderBy(p => p.CreatedDate);
    }

    protected bool HasInboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);
    }

    protected Type ResolveConsumerType(PlatformInboxBusMessage toHandleInboxMessage)
    {
        return AvailableConsumerByNameToTypeDic.GetValueOrDefault(toHandleInboxMessage.ConsumerBy, null);
    }
}
