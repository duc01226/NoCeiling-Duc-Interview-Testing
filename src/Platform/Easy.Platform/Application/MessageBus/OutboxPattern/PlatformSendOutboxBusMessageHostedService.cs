using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

/// <summary>
/// Run in an interval to scan messages in OutboxCollection in database, check new message to send it
/// </summary>
public class PlatformSendOutboxBusMessageHostedService : PlatformIntervalHostingBackgroundService
{
    private bool isProcessing;

    public PlatformSendOutboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig,
        IPlatformRootServiceProvider rootServiceProvider) : base(serviceProvider, loggerFactory)
    {
        ApplicationSettingContext = applicationSettingContext;
        OutboxConfig = outboxConfig;
        RootServiceProvider = rootServiceProvider;
    }

    public override bool LogIntervalProcessInformation => OutboxConfig.LogIntervalProcessInformation;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    protected PlatformOutboxConfig OutboxConfig { get; }

    protected IPlatformRootServiceProvider RootServiceProvider { get; }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return OutboxConfig.CheckToProcessTriggerIntervalTimeSeconds.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformPersistenceModule), Logger, $"process {GetType().Name}");

        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => SendOutboxEventBusMessages(cancellationToken),
                retryAttempt => OutboxConfig.ProcessSendMessageRetryDelaySeconds.Seconds(),
                retryCount: OutboxConfig.ProcessSendMessageRetryCount,
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    if (currentRetry >= OutboxConfig.MinimumRetrySendOutboxMessageTimesToLogError)
                        Logger.LogError(
                            ex.BeautifyStackTrace(),
                            "Retry SendOutboxEventBusMessages {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly}]",
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
                "SendOutboxEventBusMessages failed. [[Error:{Error}]]. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly}]",
                ex.Message,
                ApplicationSettingContext.ApplicationName,
                ApplicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected virtual async Task SendOutboxEventBusMessages(CancellationToken cancellationToken)
    {
        await ServiceProvider.ExecuteInjectScopedAsync(
            async (IPlatformOutboxBusMessageRepository outboxBusMessageRepository, PlatformOutboxMessageBusProducerHelper outboxMessageBusProducerHelper) =>
            {
                do
                {
                    try
                    {
                        // Random delay to prevent chance multiple api pod instance scan at the same time
                        await Task.Delay(millisecondsDelay: Random.Shared.Next(1, 10) * 1000, cancellationToken: cancellationToken);

                        var processedCanHandleMessageGroupedByTypeIdPrefixes = new HashSet<string>();

                        await Util.Pager.ExecutePagingAsync(
                            async (skipCount, pageSize) =>
                            {
                                var pagedCanHandleMessageGroupedByTypeIdPrefixes = await outboxBusMessageRepository.GetAllAsync(
                                        queryBuilder: query => query
                                            .Where(PlatformOutboxBusMessage.CanHandleMessagesExpr(OutboxConfig.MessageProcessingMaxSeconds))
                                            .Skip(skipCount)
                                            .Take(pageSize)
                                            .Select(p => p.Id),
                                        cancellationToken: cancellationToken)
                                    .Then(
                                        messageIds => messageIds
                                            .Select(PlatformOutboxBusMessage.GetIdPrefix)
                                            .Where(p => !processedCanHandleMessageGroupedByTypeIdPrefixes.Contains(p))
                                            .ToList());

                                await pagedCanHandleMessageGroupedByTypeIdPrefixes.ParallelAsync(
                                    async messageGroupedByTypeIdPrefix =>
                                    {
                                        do
                                        {
                                            try
                                            {
                                                var handlePreviousMessageFailed = false;

                                                var toHandleMessages = await PopToHandleOutboxEventBusMessages(
                                                    messageGroupedByTypeIdPrefix,
                                                    pageSize: OutboxConfig.NumberOfProcessSendOutboxMessagesSubQueuePrefetch,
                                                    cancellationToken);

                                                if (toHandleMessages.IsEmpty()) break;

                                                foreach (var toHandleOutboxMessage in toHandleMessages)
                                                    try
                                                    {
                                                        if (handlePreviousMessageFailed)
                                                            await outboxMessageBusProducerHelper.RevertExistingOutboxToNewMessageAsync(
                                                                toHandleOutboxMessage,
                                                                outboxBusMessageRepository,
                                                                cancellationToken);
                                                        else await HandleOutboxMessageAsync(toHandleOutboxMessage);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        handlePreviousMessageFailed = true;
                                                        Logger.LogError(
                                                            e.BeautifyStackTrace(),
                                                            "[PlatformSendOutboxEventBusMessageHostedService] Failed to produce outbox message. [[Error:{Error}]]" +
                                                            "Id:{OutboxMessageId} failed. " +
                                                            "JsonMessage:{OutboxJsonMessage}",
                                                            e.Message,
                                                            toHandleOutboxMessage.Id,
                                                            toHandleOutboxMessage.JsonMessage);
                                                    }

                                                if (handlePreviousMessageFailed) break;
                                            }
                                            finally
                                            {
                                                Util.GarbageCollector.Collect();
                                            }
                                        } while (true);
                                    },
                                    OutboxConfig.NumberOfProcessSendOutboxParallelMessages);

                                pagedCanHandleMessageGroupedByTypeIdPrefixes.ForEach(p => processedCanHandleMessageGroupedByTypeIdPrefixes.Add(p));
                            },
                            await outboxBusMessageRepository.CountAsync(
                                queryBuilder: query => query
                                    .Where(PlatformOutboxBusMessage.CanHandleMessagesExpr(OutboxConfig.MessageProcessingMaxSeconds)),
                                cancellationToken: cancellationToken),
                            OutboxConfig.GetCanHandleMessageGroupedByTypeIdPrefixesPageSize,
                            cancellationToken: cancellationToken);
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                } while (await AnyCanHandleOutboxBusMessages(null, outboxBusMessageRepository));
            });

        async Task HandleOutboxMessageAsync(PlatformOutboxBusMessage toHandleOutboxMessage)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                await SendMessageToBusAsync(
                    scope,
                    toHandleOutboxMessage,
                    OutboxConfig.RetryProcessFailedMessageInSecondsUnit,
                    cancellationToken);
            }
        }
    }

    protected async Task<bool> AnyCanHandleOutboxBusMessages(
        string messageGroupedByTypeIdPrefix,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        var toHandleMessages = await outboxBusMessageRepository.GetAllAsync(
            queryBuilder: query => CanHandleMessagesByProducerIdPrefixQueryBuilder(query, messageGroupedByTypeIdPrefix).Take(1));

        var result = toHandleMessages.Any() &&
                     !await outboxBusMessageRepository.AnyAsync(
                         PlatformOutboxBusMessage.CheckAnySameTypeOtherPreviousNotProcessedMessageExpr(toHandleMessages.First()));

        return result;
    }

    private IQueryable<PlatformOutboxBusMessage> CanHandleMessagesByProducerIdPrefixQueryBuilder(
        IQueryable<PlatformOutboxBusMessage> query,
        string messageGroupedByTypeIdPrefix)
    {
        return query
            .WhereIf(messageGroupedByTypeIdPrefix.IsNotNullOrEmpty(), p => p.Id.StartsWith(messageGroupedByTypeIdPrefix))
            .Where(PlatformOutboxBusMessage.CanHandleMessagesExpr(OutboxConfig.MessageProcessingMaxSeconds))
            .OrderBy(p => p.CreatedDate);
    }

    protected virtual async Task SendMessageToBusAsync(
        IServiceScope scope,
        PlatformOutboxBusMessage toHandleOutboxMessage,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
    {
        await scope.ExecuteInjectAsync(
            async (PlatformOutboxMessageBusProducerHelper outboxEventBusProducerHelper) =>
            {
                var messageType = RootServiceProvider.GetRegisteredPlatformModuleAssembliesType(toHandleOutboxMessage.MessageTypeFullName);

                if (messageType != null)
                {
                    var message = PlatformJsonSerializer.Deserialize(
                        toHandleOutboxMessage.JsonMessage,
                        messageType);

                    await outboxEventBusProducerHelper!.HandleSendingOutboxMessageAsync(
                        message,
                        toHandleOutboxMessage.RoutingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        extendedMessageIdPrefix: toHandleOutboxMessage.GetIdPrefix(),
                        handleExistingOutboxMessage: toHandleOutboxMessage,
                        sourceOutboxUowId: null,
                        cancellationToken);
                }
                else
                {
                    await outboxEventBusProducerHelper.UpdateExistingOutboxMessageFailedAsync(
                        toHandleOutboxMessage,
                        new Exception(
                            $"[{GetType().Name}] Error resolve outbox message type " +
                            $"[TypeName:{toHandleOutboxMessage.MessageTypeFullName}]. OutboxId:{toHandleOutboxMessage.Id}"),
                        retryProcessFailedMessageInSecondsUnit,
                        cancellationToken,
                        Logger);
                }
            });
    }

    protected async Task<List<PlatformOutboxBusMessage>> PopToHandleOutboxEventBusMessages(
        string messageGroupedByTypeIdPrefix,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformOutboxBusMessage>>(
                async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                {
                    if (!await AnyCanHandleOutboxBusMessages(messageGroupedByTypeIdPrefix, outboxEventBusMessageRepo)) return [];

                    var toHandleMessages = await outboxEventBusMessageRepo.GetAllAsync(
                        queryBuilder: query => CanHandleMessagesByTypeIdPrefixQueryBuilder(query, messageGroupedByTypeIdPrefix).Take(pageSize),
                        cancellationToken);

                    if (toHandleMessages.IsEmpty() ||
                        await outboxEventBusMessageRepo.AnyAsync(
                            PlatformOutboxBusMessage.CheckAnySameTypeOtherPreviousNotProcessedMessageExpr(toHandleMessages.First()),
                            cancellationToken)) return [];

                    toHandleMessages.ForEach(
                        p =>
                        {
                            p.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processing;
                            p.LastSendDate = DateTime.UtcNow;
                        });

                    await outboxEventBusMessageRepo.UpdateManyAsync(
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
                "Some other producer instance has been handling some outbox messages, which lead to row version conflict (support multi service instance running concurrently). This is as expected.");

            // WHY: Because support multiple service instance running concurrently,
            // get row version conflict is expected, so just retry again to get unprocessed outbox messages
            return await PopToHandleOutboxEventBusMessages(messageGroupedByTypeIdPrefix, pageSize, cancellationToken);
        }
    }

    protected IQueryable<PlatformOutboxBusMessage> CanHandleMessagesByTypeIdPrefixQueryBuilder(
        IQueryable<PlatformOutboxBusMessage> query,
        string messageGroupedByTypeIdPrefix)
    {
        return query
            .WhereIf(messageGroupedByTypeIdPrefix.IsNotNullOrEmpty(), p => p.Id.StartsWith(messageGroupedByTypeIdPrefix))
            .Where(PlatformOutboxBusMessage.CanHandleMessagesExpr(OutboxConfig.MessageProcessingMaxSeconds))
            .OrderBy(p => p.CreatedDate);
    }

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }
}
