using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxMessageBusProducerHelper : IPlatformHelper
{
    /// <summary>
    /// Default 40320 * 15 = 1 week total seconds
    /// </summary>
    public const int DefaultResilientRetiredCount = 40320;

    public const int DefaultResilientRetiredDelaySeconds = 15;

    private readonly IPlatformMessageBusProducer messageBusProducer;
    private readonly PlatformOutboxConfig outboxConfig;
    private readonly IPlatformRootServiceProvider rootServiceProvider;
    private readonly IServiceProvider serviceProvider;

    public PlatformOutboxMessageBusProducerHelper(
        PlatformOutboxConfig outboxConfig,
        IPlatformMessageBusProducer messageBusProducer,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        this.outboxConfig = outboxConfig;
        this.messageBusProducer = messageBusProducer;
        this.serviceProvider = serviceProvider;
        this.rootServiceProvider = rootServiceProvider;
    }

    public async Task HandleSendingOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string extendedMessageIdPrefix,
        PlatformOutboxBusMessage handleExistingOutboxMessage = null,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (serviceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null)
        {
            if (outboxConfig.StandaloneScopeForOutbox)
                await serviceProvider.ExecuteInjectScopedAsync(
                    (
                        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
                        IPlatformMessageBusProducer messageBusProducer,
                        IPlatformUnitOfWorkManager unitOfWorkManager) => SendOutboxMessageAsync(
                        message,
                        routingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        handleExistingOutboxMessage,
                        null,
                        extendedMessageIdPrefix,
                        cancellationToken,
                        CreateLogger(),
                        outboxBusMessageRepository,
                        messageBusProducer,
                        unitOfWorkManager));
            else
                await serviceProvider.ExecuteInjectAsync(
                    (
                        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
                        IPlatformMessageBusProducer messageBusProducer,
                        IPlatformUnitOfWorkManager unitOfWorkManager) => SendOutboxMessageAsync(
                        message,
                        routingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        handleExistingOutboxMessage,
                        sourceOutboxUowId,
                        extendedMessageIdPrefix,
                        cancellationToken,
                        CreateLogger(),
                        outboxBusMessageRepository,
                        messageBusProducer,
                        unitOfWorkManager));
        }
        else
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
        }
    }

    public async Task SendOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformOutboxBusMessage handleExistingOutboxMessage,
        string sourceOutboxUowId,
        string extendedMessageIdPrefix,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformUnitOfWorkManager unitOfWorkManager) where TMessage : class, new()
    {
        if (handleExistingOutboxMessage != null &&
            handleExistingOutboxMessage.SendStatus != PlatformOutboxBusMessage.SendStatuses.Processed &&
            handleExistingOutboxMessage.SendStatus != PlatformOutboxBusMessage.SendStatuses.Ignored)
            await SendExistingOutboxMessageAsync(
                handleExistingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger,
                messageBusProducer,
                outboxBusMessageRepository);
        else if (handleExistingOutboxMessage == null)
            await SaveAndTrySendNewOutboxMessageAsync(
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                sourceOutboxUowId,
                extendedMessageIdPrefix,
                cancellationToken,
                logger,
                unitOfWorkManager,
                outboxBusMessageRepository);
    }

    public async Task SendExistingOutboxMessageAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () =>
                    {
                        await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
                    },
                    sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
                    retryCount: DefaultResilientRetiredCount,
                    cancellationToken: cancellationToken)
                .Timeout(outboxConfig.MessageProcessingMaxSecondsTimeout.Seconds());

            await UpdateExistingOutboxMessageProcessedAsync(
                rootServiceProvider,
                existingOutboxMessage,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception.BeautifyStackTrace(), "SendExistingOutboxMessageAsync failed. [[Error:{Error}]]", exception.Message);

            await UpdateExistingOutboxMessageFailedAsync(existingOutboxMessage, exception, retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

    public async Task RevertExistingOutboxToNewMessageAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        CancellationToken cancellationToken)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var toUpdateMessage = await outboxBusMessageRepository.GetByIdAsync(existingOutboxMessage.Id, cancellationToken);

                await outboxBusMessageRepository.UpdateImmediatelyAsync(
                    toUpdateMessage
                        .With(p => p.SendStatus = PlatformOutboxBusMessage.SendStatuses.New),
                    cancellationToken: cancellationToken);
            },
            sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    public async Task SendExistingOutboxMessageInNewScopeAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger)
        where TMessage : class, new()
    {
        try
        {
            await rootServiceProvider.ExecuteInjectScopedAsync(
                SendExistingOutboxMessageAsync<TMessage>,
                existingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception.BeautifyStackTrace(), "SendExistingOutboxMessageInNewScopeAsync failed. [[Error:{Error}]]", exception.Message);

            await UpdateExistingOutboxMessageFailedAsync(existingOutboxMessage, exception, retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

    public static async Task UpdateExistingOutboxMessageProcessedAsync(
        IPlatformRootServiceProvider serviceProvider,
        PlatformOutboxBusMessage existingOutboxMessage,
        CancellationToken cancellationToken)
    {
        var toUpdateOutboxMessage = existingOutboxMessage;

        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                if (toUpdateOutboxMessage.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed) return;

                await serviceProvider.ExecuteInjectScopedAsync(
                    async (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                    {
                        try
                        {
                            toUpdateOutboxMessage.LastSendDate = DateTime.UtcNow;
                            toUpdateOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

                            await outboxBusMessageRepository.UpdateAsync(toUpdateOutboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
                        }
                        catch (PlatformDomainRowVersionConflictException)
                        {
                            toUpdateOutboxMessage = await serviceProvider.ExecuteInjectScopedAsync<PlatformOutboxBusMessage>(
                                (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                                    outboxBusMessageRepository.GetByIdAsync(toUpdateOutboxMessage.Id, cancellationToken));
                            throw;
                        }
                    });
            },
            sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    public async Task UpdateExistingOutboxMessageFailedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        try
        {
            logger.LogError(
                exception.BeautifyStackTrace(),
                "UpdateExistingOutboxMessageFailedAsync. [[Error:{Error}]]. [[Type:{MessageTypeFullName}]]; [[OutboxJsonMessage: {OutboxJsonMessage}]].",
                exception.Message,
                existingOutboxMessage.MessageTypeFullName,
                existingOutboxMessage.JsonMessage);

            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await rootServiceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                        {
                            var latestCurrentExistingOutboxMessage = await outboxBusMessageRepository.FirstOrDefaultAsync(
                                p => p.Id == existingOutboxMessage.Id && p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processing,
                                cancellationToken);

                            if (latestCurrentExistingOutboxMessage != null)
                                await UpdateExistingOutboxMessageFailedAsync(
                                    latestCurrentExistingOutboxMessage,
                                    exception,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    outboxBusMessageRepository);
                        });
                },
                retryCount: DefaultResilientRetiredCount,
                sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex.BeautifyStackTrace(),
                "UpdateExistingOutboxMessageFailedAsync failed. [[Error:{Error}]]].",
                ex.Message);
        }
    }

    private static async Task UpdateExistingOutboxMessageFailedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Failed;
        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.LastSendError = exception.BeautifyStackTrace().Serialize();
        existingOutboxMessage.RetriedProcessCount = (existingOutboxMessage.RetriedProcessCount ?? 0) + 1;
        existingOutboxMessage.NextRetryProcessAfter = PlatformOutboxBusMessage.CalculateNextRetryProcessAfter(
            existingOutboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await outboxBusMessageRepository.CreateOrUpdateAsync(existingOutboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
    }

    protected async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string sourceOutboxUowId,
        string extendedMessageIdPrefix,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        var (toProcessOutboxMessage, existedOutboxMessage) = await GetOrCreateToProcessOutboxMessage(
            message,
            routingKey,
            extendedMessageIdPrefix,
            cancellationToken,
            outboxBusMessageRepository);

        if (toProcessOutboxMessage == null) return;

        if (existedOutboxMessage == null ||
            existedOutboxMessage.Is(PlatformOutboxBusMessage.CanHandleMessagesExpr(outboxConfig.MessageProcessingMaxSeconds)))
        {
            var currentActiveUow = sourceOutboxUowId != null
                ? unitOfWorkManager.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId)
                : null;
            // WHY: Do not need to wait for uow completed if the uow for db do not handle actually transaction.
            // Can execute it immediately without waiting for uow to complete
            // Explain 2: (currentActiveUow.IsPseudoTransactionUow() && outboxBusMessageRepository.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId) != null) =>
            // Check to ensure that currentActiveUow is the same uow type (db context type) of outbox repository, support case outbox repo use different db context type
            if (currentActiveUow == null ||
                (currentActiveUow.IsPseudoTransactionUow() && outboxBusMessageRepository.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId) != null))
            {
                // Check try CompleteAsync current active uow if any to ensure that newOutboxMessage will be saved
                // Do this to fix if someone open uow without complete/save it for some legacy project
                if (outboxBusMessageRepository.UowManager().TryGetCurrentActiveUow() != null)
                    await outboxBusMessageRepository.UowManager().CurrentActiveUow().SaveChangesAsync(cancellationToken);

                Util.TaskRunner.QueueActionInBackground(
                    async () => await rootServiceProvider.ExecuteInjectScopedAsync(
                        SendExistingOutboxMessageAsync<TMessage>,
                        toProcessOutboxMessage,
                        message,
                        routingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        cancellationToken,
                        logger),
                    loggerFactory: CreateLogger,
                    cancellationToken: cancellationToken);
            }
            else
            {
                currentActiveUow.OnSaveChangesCompletedActions.Add(
                    async () =>
                    {
                        // Try to process sending newProcessingOutboxMessage first time immediately after task completed
                        // WHY: we can wait for the background process handle the message but try to do it
                        // immediately if possible is better instead of waiting for the background process
                        await SendExistingOutboxMessageInNewScopeAsync(
                            toProcessOutboxMessage,
                            message,
                            routingKey,
                            retryProcessFailedMessageInSecondsUnit,
                            cancellationToken,
                            logger);
                    });
            }
        }
    }

    private static async Task<(PlatformOutboxBusMessage, PlatformOutboxBusMessage)> GetOrCreateToProcessOutboxMessage<TMessage>(
        TMessage message,
        string routingKey,
        string extendedMessageIdPrefix,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository) where TMessage : class, new()
    {
        return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var messageTrackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

                var existedOutboxMessage = messageTrackId != null
                    ? await outboxBusMessageRepository.FirstOrDefaultAsync(
                        p => p.Id == PlatformOutboxBusMessage.BuildId(message.GetType(), messageTrackId, extendedMessageIdPrefix),
                        cancellationToken)
                    : null;
                var isAnySameTypeOtherNotProcessedMessage = await outboxBusMessageRepository.AnyAsync(
                    PlatformOutboxBusMessage.CheckAnySameTypeOtherPreviousNotProcessedMessageExpr(
                        message.GetType(),
                        messageTrackId,
                        existedOutboxMessage?.CreatedDate ?? Clock.UtcNow,
                        extendedMessageIdPrefix),
                    cancellationToken);

                var newOutboxMessage = existedOutboxMessage == null
                    ? await outboxBusMessageRepository.CreateAsync(
                        PlatformOutboxBusMessage.Create(
                            message,
                            messageTrackId,
                            routingKey,
                            isAnySameTypeOtherNotProcessedMessage ? PlatformOutboxBusMessage.SendStatuses.New : PlatformOutboxBusMessage.SendStatuses.Processing,
                            extendedMessageIdPrefix,
                            null),
                        dismissSendEvent: true,
                        eventCustomConfig: null,
                        cancellationToken)
                    : null;

                var toProcessOutboxMessage = isAnySameTypeOtherNotProcessedMessage
                    ? null
                    : existedOutboxMessage ?? newOutboxMessage;

                return (toProcessOutboxMessage, existedOutboxMessage);
            },
            _ => DefaultResilientRetiredDelaySeconds.Seconds(),
            DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    protected ILogger CreateLogger()
    {
        return rootServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(PlatformOutboxMessageBusProducerHelper).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}");
    }
}
