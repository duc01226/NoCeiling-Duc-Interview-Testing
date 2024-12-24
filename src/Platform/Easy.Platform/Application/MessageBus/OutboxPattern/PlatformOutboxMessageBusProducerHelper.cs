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

/// <summary>
/// Provides helper methods for implementing the Outbox Pattern with message bus producers.
/// The Outbox Pattern ensures reliable message delivery by storing messages in a database before sending them to the message bus.
/// </summary>
public class PlatformOutboxMessageBusProducerHelper : IPlatformHelper
{
    /// <summary>
    /// The default number of retry attempts for resilient operations, equivalent to approximately one day with a 1-second delay between retries.
    /// </summary>
    public const int DefaultResilientRetiredCount = 43200;

    /// <summary>
    /// The default delay in seconds between retry attempts for resilient operations.
    /// </summary>
    public const int DefaultResilientRetiredDelaySeconds = 1;

    private readonly IPlatformMessageBusProducer messageBusProducer;
    private readonly PlatformOutboxConfig outboxConfig;
    private readonly IPlatformRootServiceProvider rootServiceProvider;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformOutboxMessageBusProducerHelper" /> class.
    /// </summary>
    /// <param name="outboxConfig">The configuration for the outbox pattern.</param>
    /// <param name="messageBusProducer">The message bus producer used for sending messages.</param>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="rootServiceProvider">The root service provider.</param>
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

    /// <summary>
    /// Handles the sending of an outbox message, ensuring reliable delivery.
    /// This method checks for existing outbox messages and handles them accordingly, or creates a new outbox message and attempts to send it.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being sent.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="handleExistingOutboxMessage">An existing outbox message to handle, if applicable.</param>
    /// <param name="sourceOutboxUowId">The ID of the unit of work that originated the outbox message.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task HandleSendingOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string subQueueMessageIdPrefix,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        PlatformOutboxBusMessage handleExistingOutboxMessage = null,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        // If an outbox message repository is configured, use the outbox pattern for reliable message sending.
        if (serviceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null)
        {
            // If standalone scope for outbox is enabled, execute the sending logic in a new scope.
            if (outboxConfig.StandaloneScopeForOutbox)
            {
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
                        subQueueMessageIdPrefix,
                        needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                        cancellationToken,
                        CreateLogger(),
                        outboxBusMessageRepository,
                        messageBusProducer,
                        unitOfWorkManager));
            }
            // Otherwise, execute the sending logic in the current scope.
            else
            {
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
                        subQueueMessageIdPrefix,
                        needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                        cancellationToken,
                        CreateLogger(),
                        outboxBusMessageRepository,
                        messageBusProducer,
                        unitOfWorkManager));
            }
        }
        // If no outbox message repository is configured, send the message directly to the message bus.
        else
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
    }

    /// <summary>
    /// Sends an outbox message, either an existing one or a new one.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being sent.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="handleExistingOutboxMessage">An existing outbox message to handle, if applicable.</param>
    /// <param name="sourceOutboxUowId">The ID of the unit of work that originated the outbox message.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="outboxBusMessageRepository">The repository for accessing outbox messages.</param>
    /// <param name="messageBusProducer">The message bus producer used for sending messages.</param>
    /// <param name="unitOfWorkManager">The unit of work manager.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SendOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformOutboxBusMessage handleExistingOutboxMessage,
        string sourceOutboxUowId,
        string subQueueMessageIdPrefix,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformUnitOfWorkManager unitOfWorkManager) where TMessage : class, new()
    {
        // If there's an existing outbox message that's not processed or ignored, send it.
        if (handleExistingOutboxMessage != null &&
            handleExistingOutboxMessage.SendStatus != PlatformOutboxBusMessage.SendStatuses.Processed &&
            handleExistingOutboxMessage.SendStatus != PlatformOutboxBusMessage.SendStatuses.Ignored)
        {
            await SendExistingOutboxMessageAsync(
                handleExistingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                cancellationToken,
                logger,
                messageBusProducer,
                outboxBusMessageRepository);
        }
        // If there's no existing outbox message, create a new one and attempt to send it.
        else if (handleExistingOutboxMessage == null)
        {
            await SaveAndTrySendNewOutboxMessageAsync(
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                sourceOutboxUowId,
                subQueueMessageIdPrefix,
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                cancellationToken,
                logger,
                unitOfWorkManager,
                outboxBusMessageRepository);
        }
    }

    /// <summary>
    /// Sends an existing outbox message.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being sent.</typeparam>
    /// <param name="existingOutboxMessage">The existing outbox message to send.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="messageBusProducer">The message bus producer used for sending messages.</param>
    /// <param name="outboxBusMessageRepository">The repository for accessing outbox messages.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SendExistingOutboxMessageAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        var startIntervalPingProcessingCts = new CancellationTokenSource();

        try
        {
            // If sub-queueing is enabled and there are other unprocessed messages with the same prefix, revert the existing message to "New".
            if (needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage &&
                PlatformOutboxBusMessage.GetSubQueuePrefix(existingOutboxMessage.Id).IsNotNullOrEmpty() &&
                await outboxBusMessageRepository.AnyAsync(
                    PlatformOutboxBusMessage.CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(existingOutboxMessage),
                    cancellationToken))
                await RevertExistingOutboxToNewMessageAsync(existingOutboxMessage, outboxBusMessageRepository, cancellationToken);
            else
            {
                StartIntervalPingProcessing(
                    existingOutboxMessage,
                    loggerFactory: () => logger,
                    startIntervalPingProcessingCts.Token);

                // Retry sending the message multiple times in case of transient errors.
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () =>
                    {
                        await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
                    },
                    sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
                    retryCount: DefaultResilientRetiredCount,
                    cancellationToken: cancellationToken);

                await startIntervalPingProcessingCts.CancelAsync();

                // Update the outbox message as processed after successful sending.
                await UpdateExistingOutboxMessageProcessedAsync(
                    rootServiceProvider,
                    existingOutboxMessage,
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            await startIntervalPingProcessingCts.CancelAsync();

            logger.LogError(exception.BeautifyStackTrace(), "SendExistingOutboxMessageAsync failed. [[Error:{Error}]]", exception.Message);

            // Update the outbox message as failed if sending fails.
            await UpdateExistingOutboxMessageFailedAsync(existingOutboxMessage, exception, retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
        finally
        {
            startIntervalPingProcessingCts.Dispose();
        }
    }

    private void StartIntervalPingProcessing(
        PlatformOutboxBusMessage existingOutboxMessage,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken)
    {
        Util.TaskRunner.QueueActionInBackground(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        async () =>
                        {
                            try
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    await rootServiceProvider.ExecuteInjectScopedAsync(
                                        async (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                                        {
                                            using (var uow = outboxBusMessageRepository.UowManager().Begin())
                                            {
                                                var toUpdateExistingOutboxMessage = await outboxBusMessageRepository.GetByIdAsync(
                                                    existingOutboxMessage.Id,
                                                    cancellationToken);

                                                if (!cancellationToken.IsCancellationRequested)
                                                {
                                                    await outboxBusMessageRepository.SetAsync(
                                                        toUpdateExistingOutboxMessage.With(p => p.LastProcessingPingDate = Clock.UtcNow),
                                                        cancellationToken: cancellationToken);

                                                    existingOutboxMessage.LastProcessingPingDate = toUpdateExistingOutboxMessage.LastProcessingPingDate;
                                                }

                                                await uow.CompleteAsync(cancellationToken);
                                            }
                                        });
                                }

                                await Task.Delay(PlatformOutboxBusMessage.CheckProcessingPingIntervalSeconds.Seconds(), cancellationToken);
                            }
                            catch (TaskCanceledException)
                            {
                                // Empty and skip taskCanceledException
                            }
                        },
                        cancellationToken: cancellationToken,
                        retryCount: 100,
                        onRetry: (ex, delayRetryTime, retryAttempt, context) =>
                        {
                            if (retryAttempt > 10) loggerFactory().LogError(ex.BeautifyStackTrace(), "Update PlatformOutboxBusMessage LastProcessingPingTime failed");
                        });
                }
            },
            loggerFactory: loggerFactory,
            delayTimeSeconds: PlatformOutboxBusMessage.CheckProcessingPingIntervalSeconds,
            cancellationToken: CancellationToken.None,
            logFullStackTraceBeforeBackgroundTask: false);
    }

    /// <summary>
    /// Reverts an existing outbox message to the "New" state.
    /// </summary>
    /// <param name="existingOutboxMessage">The existing outbox message to revert.</param>
    /// <param name="outboxBusMessageRepository">The repository for accessing outbox messages.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task RevertExistingOutboxToNewMessageAsync(
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

    /// <summary>
    /// Sends an existing outbox message in a new scope.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being sent.</typeparam>
    /// <param name="existingOutboxMessage">The existing outbox message to send.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SendExistingOutboxMessageInNewScopeAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
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
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                cancellationToken,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception.BeautifyStackTrace(), "SendExistingOutboxMessageInNewScopeAsync failed. [[Error:{Error}]]", exception.Message);

            // Update the outbox message as failed if sending fails.
            await UpdateExistingOutboxMessageFailedAsync(existingOutboxMessage, exception, retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

    /// <summary>
    /// Updates an existing outbox message as processed.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="existingOutboxMessage">The existing outbox message to update.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
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
                        await outboxBusMessageRepository.UowManager()
                            .ExecuteUowTask(
                                async () =>
                                {
                                    outboxBusMessageRepository.UowManager()
                                        .CurrentActiveUow()
                                        .SetCachedExistingOriginalEntity<PlatformOutboxBusMessage, string>(toUpdateOutboxMessage, null, true);

                                    try
                                    {
                                        toUpdateOutboxMessage.LastSendDate = DateTime.UtcNow;
                                        toUpdateOutboxMessage.LastProcessingPingDate = DateTime.UtcNow;
                                        toUpdateOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

                                        await outboxBusMessageRepository.SetAsync(toUpdateOutboxMessage, cancellationToken);
                                    }
                                    catch (PlatformDomainRowVersionConflictException)
                                    {
                                        // If a concurrency conflict occurs, retrieve the latest version of the message and retry.
                                        toUpdateOutboxMessage = await serviceProvider.ExecuteInjectScopedAsync<PlatformOutboxBusMessage>(
                                            (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                                                outboxBusMessageRepository.GetByIdAsync(toUpdateOutboxMessage.Id, cancellationToken));
                                        throw;
                                    }
                                });
                    });
            },
            sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates an existing outbox message as failed.
    /// </summary>
    /// <param name="existingOutboxMessage">The existing outbox message to update.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
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
                            // Retrieve the latest version of the outbox message to prevent concurrency issues.
                            var latestCurrentExistingOutboxMessage = await outboxBusMessageRepository.FirstOrDefaultAsync(
                                p => p.Id == existingOutboxMessage.Id && p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processing,
                                cancellationToken);

                            if (latestCurrentExistingOutboxMessage != null)
                            {
                                await UpdateExistingOutboxMessageFailedAsync(
                                    latestCurrentExistingOutboxMessage,
                                    exception,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    outboxBusMessageRepository);
                            }
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

    /// <summary>
    /// Updates an existing outbox message with failure details.
    /// </summary>
    /// <param name="existingOutboxMessage">The existing outbox message to update.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="outboxBusMessageRepository">The repository for accessing outbox messages.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    private static async Task UpdateExistingOutboxMessageFailedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Failed;
        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.LastProcessingPingDate = DateTime.UtcNow;
        existingOutboxMessage.LastSendError = exception.BeautifyStackTrace().Serialize();
        existingOutboxMessage.RetriedProcessCount = (existingOutboxMessage.RetriedProcessCount ?? 0) + 1;
        existingOutboxMessage.NextRetryProcessAfter = PlatformOutboxBusMessage.CalculateNextRetryProcessAfter(
            existingOutboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await outboxBusMessageRepository.SetAsync(existingOutboxMessage, cancellationToken);
    }

    /// <summary>
    /// Saves a new outbox message and attempts to send it.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being sent.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message sending.</param>
    /// <param name="sourceOutboxUowId">The ID of the unit of work that originated the outbox message.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="unitOfWorkManager">The unit of work manager.</param>
    /// <param name="outboxBusMessageRepository">The repository for accessing outbox messages.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    protected async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string sourceOutboxUowId,
        string subQueueMessageIdPrefix,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        // Get the sourceOutboxUow active unit of work, if applicable.
        var sourceOutboxUowActiveUow = sourceOutboxUowId != null
            ? unitOfWorkManager.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId)
            : null;

        // If there's no active unit of work or the unit of work is a pseudo transaction, send the message immediately in a background thread.
        var canSendMessageDirectlyWithoutWaitUowTransaction = sourceOutboxUowActiveUow == null || sourceOutboxUowActiveUow.IsPseudoTransactionUow();

        // Try to send directly first without using outbox for faster performance if no uow or fake uow. If failed => use inbox to support retry later
        if (canSendMessageDirectlyWithoutWaitUowTransaction)
        {
            try
            {
                await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
            }
            catch (Exception)
            {
                await DoCreateNewInboxAndSendMessage();
            }
        }
        else
            await DoCreateNewInboxAndSendMessage();


        async Task DoCreateNewInboxAndSendMessage()
        {
            // Get or create the outbox message to process.
            var toProcessOutboxMessage = await GetOrCreateToProcessOutboxMessage(
                message,
                routingKey,
                subQueueMessageIdPrefix,
                cancellationToken,
                outboxBusMessageRepository);

            if (toProcessOutboxMessage != null)
            {
                // If there's no active unit of work or the unit of work is a pseudo transaction, send the message immediately in a background thread.
                if (canSendMessageDirectlyWithoutWaitUowTransaction)
                {
                    // If there's an active unit of work, save changes to ensure the outbox message is persisted.
                    await outboxBusMessageRepository.UowManager().TryCurrentActiveUowSaveChangesAsync();

                    Util.TaskRunner.QueueActionInBackground(
                        async () => await rootServiceProvider.ExecuteInjectScopedAsync(
                            SendExistingOutboxMessageAsync<TMessage>,
                            toProcessOutboxMessage,
                            message,
                            routingKey,
                            retryProcessFailedMessageInSecondsUnit,
                            needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                            cancellationToken,
                            logger),
                        loggerFactory: CreateLogger,
                        cancellationToken: cancellationToken,
                        logFullStackTraceBeforeBackgroundTask: false);
                }
                else
                {
                    // If there's an active unit of work, add an action to send the message after the unit of work completes.
                    sourceOutboxUowActiveUow!.OnSaveChangesCompletedActions.Add(
                        async () =>
                        {
                            // Try to process sending the outbox message immediately after the unit of work completes.
                            // Execute task in background separated thread task
                            Util.TaskRunner.QueueActionInBackground(
                                async () =>
                                {
                                    await SendExistingOutboxMessageInNewScopeAsync(
                                        toProcessOutboxMessage,
                                        message,
                                        routingKey,
                                        retryProcessFailedMessageInSecondsUnit,
                                        needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                                        cancellationToken,
                                        logger);
                                },
                                cancellationToken: cancellationToken);
                        });
                }
            }
        }
    }

    /// <summary>
    /// Retrieves or creates an outbox message to be processed.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being sent.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <param name="outboxBusMessageRepository">The repository for accessing outbox messages.</param>
    private static async Task<PlatformOutboxBusMessage?> GetOrCreateToProcessOutboxMessage<TMessage>(
        TMessage message,
        string routingKey,
        string subQueueMessageIdPrefix,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository) where TMessage : class, new()
    {
        return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var messageTrackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

                // Check if an outbox message with the same tracking ID and sub-queue message ID prefix already exists.
                var existedOutboxMessage = messageTrackId != null
                    ? await outboxBusMessageRepository.FirstOrDefaultAsync(
                        p => p.Id == PlatformOutboxBusMessage.BuildId(message.GetType(), messageTrackId, subQueueMessageIdPrefix),
                        cancellationToken)
                    : null;

                // Check if there are any other unprocessed messages with the same sub-queue message ID prefix.
                var isAnySameMessageTypeAndIdPrefixOtherNotProcessedMessage =
                    subQueueMessageIdPrefix.IsNotNullOrEmpty() &&
                    await outboxBusMessageRepository.AnyAsync(
                        PlatformOutboxBusMessage.CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
                            message.GetType(),
                            messageTrackId,
                            existedOutboxMessage?.CreatedDate ?? Clock.UtcNow,
                            subQueueMessageIdPrefix),
                        cancellationToken);

                // If no existing message is found, create a new one.
                var newOutboxMessage = existedOutboxMessage == null
                    ? await outboxBusMessageRepository.CreateAsync(
                        PlatformOutboxBusMessage.Create(
                            message,
                            messageTrackId,
                            routingKey,
                            isAnySameMessageTypeAndIdPrefixOtherNotProcessedMessage
                                ? PlatformOutboxBusMessage.SendStatuses.New
                                : PlatformOutboxBusMessage.SendStatuses.Processing,
                            subQueueMessageIdPrefix,
                            null),
                        dismissSendEvent: true,
                        eventCustomConfig: null,
                        cancellationToken)
                    : null;

                // Determine the message to process based on whether there are other unprocessed messages with the same prefix.
                // existed message exist and can't be handling right now
                // Then should not process message => return null
                var toProcessOutboxMessage = isAnySameMessageTypeAndIdPrefixOtherNotProcessedMessage ||
                                             existedOutboxMessage?.Is(PlatformOutboxBusMessage.CanHandleMessagesExpr()) == false
                    ? null
                    : existedOutboxMessage ?? newOutboxMessage;

                return toProcessOutboxMessage;
            },
            _ => DefaultResilientRetiredDelaySeconds.Seconds(),
            DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a logger for this helper class.
    /// </summary>
    /// <returns>A logger instance.</returns>
    protected ILogger CreateLogger()
    {
        return rootServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(PlatformOutboxMessageBusProducerHelper).GetNameOrGenericTypeName() + $"-{GetType().Name}");
    }
}
