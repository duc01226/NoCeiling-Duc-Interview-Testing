using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

/// <summary>
/// Provides helper methods for implementing the Inbox Pattern with message bus consumers.
/// The Inbox Pattern helps prevent duplicate message processing by storing consumed messages in a database.
/// </summary>
public static class PlatformInboxMessageBusConsumerHelper
{
    /// <summary>
    /// The default number of retry attempts for resilient operations, equivalent to approximately one week with a 15-second delay between retries.
    /// </summary>
    public const int DefaultResilientRetiredCount = 40320;

    /// <summary>
    /// The default delay in seconds between retry attempts for resilient operations.
    /// </summary>
    public const int DefaultResilientRetiredDelaySeconds = 15;

    /// <summary>
    /// Handles the execution of an inbox consumer, ensuring that messages are processed only once.
    /// This method checks for existing inbox messages and handles them accordingly, or creates a new inbox message and attempts to consume it.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="rootServiceProvider">The root service provider.</param>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="consumerType">The type of the consumer handling the message.</param>
    /// <param name="inboxBusMessageRepository">The repository for accessing inbox messages.</param>
    /// <param name="inboxConfig">The configuration for the inbox pattern.</param>
    /// <param name="applicationSettingContext">applicationSettingContext</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="forApplicationName">The name of the application the message is intended for.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message processing.</param>
    /// <param name="handleExistingInboxMessage">An existing inbox message to handle, if applicable.</param>
    /// <param name="handleExistingInboxMessageConsumerInstance">The consumer instance to use for handling an existing inbox message.</param>
    /// <param name="handleInUow">The unit of work to use for handling the message.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="autoDeleteProcessedMessageImmediately">Indicates whether processed messages should be deleted immediately.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task HandleExecutingInboxConsumerAsync<TMessage>(
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider,
        Type consumerType,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        PlatformInboxConfig inboxConfig,
        IPlatformApplicationSettingContext applicationSettingContext,
        TMessage message,
        string forApplicationName,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformInboxBusMessage handleExistingInboxMessage,
        IPlatformApplicationMessageBusConsumer<TMessage> handleExistingInboxMessageConsumerInstance,
        IPlatformUnitOfWork handleInUow,
        string subQueueMessageIdPrefix,
        bool autoDeleteProcessedMessageImmediately = false,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage = true,
        bool allowHandleNewInboxMessageInBackground = false,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        // If there's an existing inbox message that's not processed or ignored, handle it directly.
        if (handleExistingInboxMessage != null &&
            handleExistingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed &&
            handleExistingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Ignored)
            await HandleConsumerLogicDirectlyForExistingInboxMessage(
                handleExistingInboxMessage,
                handleExistingInboxMessageConsumerInstance,
                serviceProvider,
                inboxBusMessageRepository,
                message,
                routingKey,
                loggerFactory,
                retryProcessFailedMessageInSecondsUnit,
                autoDeleteProcessedMessageImmediately,
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                cancellationToken);
        // If there's no existing inbox message, create a new one and attempt to consume it.
        else if (handleExistingInboxMessage == null)
            await SaveAndTryConsumeNewInboxMessageAsync(
                rootServiceProvider,
                consumerType,
                inboxBusMessageRepository,
                applicationSettingContext,
                message,
                forApplicationName,
                routingKey,
                loggerFactory,
                handleInUow,
                autoDeleteProcessedMessageImmediately,
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                subQueueMessageIdPrefix,
                retryProcessFailedMessageInSecondsUnit,
                allowHandleNewInboxMessageInBackground,
                cancellationToken);
    }

    /// <summary>
    /// Saves a new inbox message and attempts to consume it.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="rootServiceProvider">The root service provider.</param>
    /// <param name="consumerType">The type of the consumer handling the message.</param>
    /// <param name="inboxBusMessageRepository">The repository for accessing inbox messages.</param>
    /// <param name="applicationSettingContext">applicationSettingContext</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="forApplicationName">The name of the application the message is intended for.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="handleInUow">The unit of work to use for handling the message.</param>
    /// <param name="autoDeleteProcessedMessage">Indicates whether processed messages should be deleted immediately.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message processing.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    private static async Task SaveAndTryConsumeNewInboxMessageAsync<TMessage>(
        IPlatformRootServiceProvider rootServiceProvider,
        Type consumerType,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        IPlatformApplicationSettingContext applicationSettingContext,
        TMessage message,
        string forApplicationName,
        string routingKey,
        Func<ILogger> loggerFactory,
        IPlatformUnitOfWork handleInUow,
        bool autoDeleteProcessedMessage,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        string subQueueMessageIdPrefix,
        double retryProcessFailedMessageInSecondsUnit,
        bool allowHandleNewInboxMessageInBackground,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        // Get or create the inbox message to process.
        var (toProcessInboxMessage, _) =
            await GetOrCreateToProcessInboxMessage(
                consumerType,
                inboxBusMessageRepository,
                message,
                forApplicationName,
                routingKey,
                subQueueMessageIdPrefix,
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
                applicationSettingContext,
                cancellationToken);

        if (toProcessInboxMessage != null)
        {
            // If a unit of work is provided and it's not a pseudo transaction, execute the consumer within the unit of work.
            if (handleInUow != null && !handleInUow.IsPseudoTransactionUow())
            {
                handleInUow.OnSaveChangesCompletedActions.Add(
                    async () => await ExecuteConsumerForNewInboxMessage(
                        rootServiceProvider,
                        consumerType,
                        message,
                        toProcessInboxMessage,
                        routingKey,
                        autoDeleteProcessedMessage,
                        retryProcessFailedMessageInSecondsUnit,
                        loggerFactory,
                        cancellationToken));
            }
            else
            {
                // If there's an active unit of work, save changes to ensure the inbox message is persisted.
                if (inboxBusMessageRepository.UowManager().TryGetCurrentActiveUow() != null)
                    await inboxBusMessageRepository.UowManager().CurrentActiveUow().SaveChangesAsync(cancellationToken);

                if (allowHandleNewInboxMessageInBackground)
                    Util.TaskRunner.QueueActionInBackground(
                        async () =>
                        {
                            await ExecuteConsumerForNewInboxMessage(
                                rootServiceProvider,
                                consumerType,
                                message,
                                toProcessInboxMessage,
                                routingKey,
                                autoDeleteProcessedMessage,
                                retryProcessFailedMessageInSecondsUnit,
                                loggerFactory,
                                cancellationToken);
                        },
                        loggerFactory,
                        cancellationToken: CancellationToken.None);
                else
                    await ExecuteConsumerForNewInboxMessage(
                        rootServiceProvider,
                        consumerType,
                        message,
                        toProcessInboxMessage,
                        routingKey,
                        autoDeleteProcessedMessage,
                        retryProcessFailedMessageInSecondsUnit,
                        loggerFactory,
                        cancellationToken);
            }
        }
    }

    /// <summary>
    /// Retrieves or creates an inbox message to be processed. Return (toProcessInboxMessage, existedInboxMessage)
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="consumerType">The type of the consumer handling the message.</param>
    /// <param name="inboxBusMessageRepository">The repository for accessing inbox messages.</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="forApplicationName">The name of the application the message is intended for.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="applicationSettingContext">applicationSettingContext</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    private static async Task<(PlatformInboxBusMessage?, PlatformInboxBusMessage?)> GetOrCreateToProcessInboxMessage<TMessage>(
        Type consumerType,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        TMessage message,
        string forApplicationName,
        string routingKey,
        string subQueueMessageIdPrefix,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        IPlatformApplicationSettingContext applicationSettingContext,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var trackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

                // Check if an inbox message with the same tracking ID and sub-queue message ID prefix already exists.
                var existedInboxMessage = trackId != null
                    ? await inboxBusMessageRepository.FirstOrDefaultAsync(
                        p => p.Id == PlatformInboxBusMessage.BuildId(consumerType, trackId, subQueueMessageIdPrefix),
                        cancellationToken)
                    : null;

                // Check if there are any other unprocessed messages with the same sub-queue message ID prefix.
                var isAnySameConsumerMessageIdPrefixOtherNotProcessedMessage =
                    subQueueMessageIdPrefix.IsNotNullOrEmpty() &&
                    needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage &&
                    await inboxBusMessageRepository.AnyAsync(
                        PlatformInboxBusMessage.CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
                            consumerType,
                            trackId,
                            existedInboxMessage?.CreatedDate ?? Clock.UtcNow,
                            subQueueMessageIdPrefix),
                        cancellationToken);

                // If no existing message is found, create a new one.
                var newInboxMessage = existedInboxMessage == null
                    ? await CreateNewInboxMessageAsync(
                        inboxBusMessageRepository,
                        consumerType,
                        message,
                        routingKey,
                        isAnySameConsumerMessageIdPrefixOtherNotProcessedMessage
                            ? PlatformInboxBusMessage.ConsumeStatuses.New
                            : PlatformInboxBusMessage.ConsumeStatuses.Processing,
                        forApplicationName,
                        subQueueMessageIdPrefix,
                        cancellationToken)
                    : null;

                // Determine the message to process based on whether there are other unprocessed messages with the same prefix OR
                // existed message exist and can't be handling right now
                // Then should not process message => return null
                var toProcessInboxMessage =
                    isAnySameConsumerMessageIdPrefixOtherNotProcessedMessage ||
                    existedInboxMessage?.Is(PlatformInboxBusMessage.CanHandleMessagesExpr(applicationSettingContext.ApplicationName)) == false
                        ? null
                        : existedInboxMessage ?? newInboxMessage;

                return (toProcessInboxMessage, existedInboxMessage);
            },
            _ => DefaultResilientRetiredDelaySeconds.Seconds(),
            DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes the consumer logic for a new inbox message.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="rootServiceProvider">The root service provider.</param>
    /// <param name="consumerType">The type of the consumer handling the message.</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="newInboxMessage">The new inbox message to process.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="autoDeleteProcessedMessage">Indicates whether processed messages should be deleted immediately.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message processing.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task ExecuteConsumerForNewInboxMessage<TMessage>(
        IPlatformRootServiceProvider rootServiceProvider,
        Type consumerType,
        TMessage message,
        PlatformInboxBusMessage newInboxMessage,
        string routingKey,
        bool autoDeleteProcessedMessage,
        double retryProcessFailedMessageInSecondsUnit,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        await rootServiceProvider.ExecuteInjectScopedAsync(
            async (IServiceProvider serviceProvider) =>
            {
                try
                {
                    // Resolve the consumer instance and configure it for inbox message handling.
                    var consumer = serviceProvider.GetService(consumerType)
                        .Cast<IPlatformApplicationMessageBusConsumer<TMessage>>()
                        .With(uow => uow.HandleExistingInboxMessage = newInboxMessage)
                        .With(uow => uow.NeedToCheckAnySameConsumerOtherPreviousNotProcessedInboxMessage = false)
                        .With(uow => uow.AutoDeleteProcessedInboxEventMessageImmediately = autoDeleteProcessedMessage);

                    // Execute the consumer's HandleAsync method with a timeout.
                    await consumer
                        .HandleAsync(message, routingKey);
                }
                catch (Exception ex)
                {
                    // If an error occurs during consumer execution, update the inbox message as failed.
                    await UpdateExistingInboxFailedMessageAsync(
                        serviceProvider,
                        newInboxMessage,
                        message,
                        consumerType,
                        ex,
                        retryProcessFailedMessageInSecondsUnit,
                        loggerFactory,
                        cancellationToken);
                }
            });
    }

    /// <summary>
    /// Handles the consumer logic directly for an existing inbox message.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="existingInboxMessage">The existing inbox message to handle.</param>
    /// <param name="consumer">The consumer instance to use for handling the message.</param>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="inboxBusMessageRepository">The repository for accessing inbox messages.</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message processing.</param>
    /// <param name="autoDeleteProcessedMessage">Indicates whether processed messages should be deleted immediately.</param>
    /// <param name="needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage">Indicates whether to check for other unprocessed messages with the same sub-queue message ID prefix.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task HandleConsumerLogicDirectlyForExistingInboxMessage<TMessage>(
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformApplicationMessageBusConsumer<TMessage> consumer,
        IServiceProvider serviceProvider,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        bool autoDeleteProcessedMessage,
        bool needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        var startIntervalPingProcessingCts = new CancellationTokenSource();

        try
        {
            // If sub-queueing is enabled and there are other unprocessed messages with the same prefix, revert the existing message to "New".
            if (needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage &&
                PlatformInboxBusMessage.GetSubQueuePrefix(existingInboxMessage.Id).IsNotNullOrEmpty() &&
                await inboxBusMessageRepository.AnyAsync(
                    PlatformInboxBusMessage.CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(existingInboxMessage),
                    cancellationToken))
            {
                await RevertExistingInboxToNewMessageAsync(existingInboxMessage, inboxBusMessageRepository, cancellationToken);
            }
            else
            {
                StartIntervalPingProcessing(
                    existingInboxMessage,
                    loggerFactory,
                    serviceProvider,
                    startIntervalPingProcessingCts.Token);

                // Execute the consumer's HandleAsync method with a timeout.
                await consumer
                    .With(uow => uow.IsHandlingLogicForInboxMessage = true)
                    .With(b => b.AutoDeleteProcessedInboxEventMessageImmediately = autoDeleteProcessedMessage)
                    .HandleAsync(message, routingKey);

                try
                {
                    await startIntervalPingProcessingCts.CancelAsync();

                    // Update the inbox message as processed.
                    await UpdateExistingInboxProcessedMessageAsync(
                        serviceProvider.GetRequiredService<IPlatformRootServiceProvider>(),
                        existingInboxMessage,
                        cancellationToken);

                    // If auto-deletion is enabled, delete the processed message.
                    if (autoDeleteProcessedMessage)
                        await DeleteExistingInboxProcessedMessageAsync(
                            serviceProvider,
                            existingInboxMessage,
                            loggerFactory,
                            cancellationToken);
                }
                catch (Exception)
                {
                    // If an error occurs during updating the processed message, retry updating by ID.
                    await UpdateExistingInboxProcessedMessageAsync(
                        serviceProvider,
                        existingInboxMessage.Id,
                        loggerFactory,
                        cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            await startIntervalPingProcessingCts.CancelAsync();

            // If an error occurs during consumer execution, update the inbox message as failed.
            await UpdateExistingInboxFailedMessageAsync(
                serviceProvider,
                existingInboxMessage,
                message,
                consumer.GetType(),
                ex,
                retryProcessFailedMessageInSecondsUnit,
                loggerFactory,
                cancellationToken);
        }
        finally
        {
            startIntervalPingProcessingCts.Dispose();
        }
    }

    private static void StartIntervalPingProcessing(
        PlatformInboxBusMessage existingInboxMessage,
        Func<ILogger> loggerFactory,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Use root provider to prevent disposed service provider error when run in background
        var rootServiceProvider = serviceProvider.GetRequiredService<IPlatformRootServiceProvider>();

        Util.TaskRunner.QueueActionInBackground(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        async () =>
                        {
                            try
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                    await rootServiceProvider.ExecuteInjectScopedAsync(
                                        async (IPlatformInboxBusMessageRepository inboxBusMessageRepository) =>
                                        {
                                            var toUpdateExistingInboxMessage = await inboxBusMessageRepository.GetByIdAsync(existingInboxMessage.Id, cancellationToken);

                                            if (!cancellationToken.IsCancellationRequested)
                                            {
                                                await inboxBusMessageRepository.SetAsync(
                                                    toUpdateExistingInboxMessage.With(p => p.LastProcessingPingDate = Clock.UtcNow),
                                                    cancellationToken: cancellationToken);

                                                existingInboxMessage.LastProcessingPingDate = toUpdateExistingInboxMessage.LastProcessingPingDate;
                                            }
                                        });

                                await Task.Delay(PlatformInboxBusMessage.CheckProcessingPingIntervalSeconds.Seconds(), cancellationToken);
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
                            if (retryAttempt > 10) loggerFactory().LogError(ex.BeautifyStackTrace(), "Update PlatformInboxBusMessage LastProcessingPingTime failed");
                        });
            },
            loggerFactory,
            delayTimeSeconds: PlatformInboxBusMessage.CheckProcessingPingIntervalSeconds,
            cancellationToken: CancellationToken.None,
            logFullStackTraceBeforeBackgroundTask: false);
    }

    /// <summary>
    /// Reverts an existing inbox message to the "New" state.
    /// </summary>
    /// <param name="existingInboxMessage">The existing inbox message to revert.</param>
    /// <param name="inboxBusMessageRepository">The repository for accessing inbox messages.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task RevertExistingInboxToNewMessageAsync(
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        CancellationToken cancellationToken)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var toUpdateMessage = await inboxBusMessageRepository.GetByIdAsync(existingInboxMessage.Id, cancellationToken);

                await inboxBusMessageRepository.UpdateImmediatelyAsync(
                    toUpdateMessage
                        .With(p => p.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.New),
                    cancellationToken: cancellationToken);
            },
            sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a new inbox message in the database.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="inboxBusMessageRepository">The repository for accessing inbox messages.</param>
    /// <param name="consumerType">The type of the consumer handling the message.</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="consumeStatus">The initial consume status of the inbox message.</param>
    /// <param name="forApplicationName">The name of the application the message is intended for.</param>
    /// <param name="subQueueMessageIdPrefix">A prefix for the message ID, used for sub-queueing.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task<PlatformInboxBusMessage> CreateNewInboxMessageAsync<TMessage>(
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        Type consumerType,
        TMessage message,
        string routingKey,
        PlatformInboxBusMessage.ConsumeStatuses consumeStatus,
        string forApplicationName,
        string subQueueMessageIdPrefix,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        var newInboxMessage = PlatformInboxBusMessage.Create(
            message,
            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
            message.As<IPlatformTrackableBusMessage>()?.ProduceFrom,
            routingKey,
            consumerType,
            consumeStatus,
            forApplicationName,
            subQueueMessageIdPrefix);

        var result = await inboxBusMessageRepository.CreateImmediatelyAsync(
            newInboxMessage,
            dismissSendEvent: true,
            eventCustomConfig: null,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Updates an existing inbox message as processed.
    /// </summary>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="existingInboxMessageId">The ID of the existing inbox message to update.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task UpdateExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        string existingInboxMessageId,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                    await serviceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                        {
                            var existingInboxMessage = await inboxBusMessageRepo.FirstOrDefaultAsync(
                                predicate: p => p.Id == existingInboxMessageId,
                                cancellationToken: cancellationToken);

                            if (existingInboxMessage != null)
                                await UpdateExistingInboxProcessedMessageAsync(
                                    serviceProvider.GetRequiredService<IPlatformRootServiceProvider>(),
                                    existingInboxMessage,
                                    cancellationToken);
                        }),
                retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
                retryCount: DefaultResilientRetiredCount,
                onRetry: (ex, retryTime, retryAttempt, context) =>
                    LogErrorOfUpdateExistingInboxProcessedMessage(existingInboxMessageId, loggerFactory, ex),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            LogErrorOfUpdateExistingInboxProcessedMessage(existingInboxMessageId, loggerFactory, ex);
        }
    }

    /// <summary>
    /// Logs an error that occurred while updating an existing inbox message as processed.
    /// </summary>
    /// <param name="existingInboxMessageId">The ID of the existing inbox message.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="ex">The exception that occurred.</param>
    private static void LogErrorOfUpdateExistingInboxProcessedMessage(string existingInboxMessageId, Func<ILogger> loggerFactory, Exception ex)
    {
        loggerFactory()
            .LogError(
                ex.BeautifyStackTrace(),
                "UpdateExistingInboxProcessedMessageAsync failed. [[Error:{Error}]], [ExistingInboxMessageId:{ExistingInboxMessageId}].",
                ex.Message,
                existingInboxMessageId);
    }

    /// <summary>
    /// Updates an existing inbox message as processed.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="existingInboxMessage">The existing inbox message to update.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task UpdateExistingInboxProcessedMessageAsync(
        IPlatformRootServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        CancellationToken cancellationToken = default)
    {
        var toUpdateInboxMessage = existingInboxMessage;

        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                if (toUpdateInboxMessage.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed) return;

                await serviceProvider.ExecuteInjectScopedAsync(
                    async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                    {
                        try
                        {
                            toUpdateInboxMessage.LastConsumeDate = Clock.UtcNow;
                            toUpdateInboxMessage.LastProcessingPingDate = Clock.UtcNow;
                            toUpdateInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processed;

                            await inboxBusMessageRepo.UpdateAsync(toUpdateInboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
                        }
                        catch (PlatformDomainRowVersionConflictException)
                        {
                            // If a concurrency conflict occurs, retrieve the latest version of the message and retry.
                            toUpdateInboxMessage = await serviceProvider.ExecuteInjectScopedAsync<PlatformInboxBusMessage>(
                                (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                                    inboxBusMessageRepo.GetByIdAsync(toUpdateInboxMessage.Id, cancellationToken));
                            throw;
                        }
                    });
            },
            sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes an existing inbox message that has been processed.
    /// </summary>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="existingInboxMessage">The existing inbox message to delete.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task DeleteExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await serviceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                        {
                            await inboxBusMessageRepo.DeleteManyAsync(
                                predicate: p => p.Id == existingInboxMessage.Id && p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed,
                                dismissSendEvent: true,
                                eventCustomConfig: null,
                                cancellationToken);
                        });
                },
                sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
                retryCount: DefaultResilientRetiredCount,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            loggerFactory().LogError(e.BeautifyStackTrace(), "Try DeleteExistingInboxProcessedMessageAsync failed");
        }
    }

    /// <summary>
    /// Updates an existing inbox message as failed.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="existingInboxMessage">The existing inbox message to update.</param>
    /// <param name="message">The message being consumed.</param>
    /// <param name="consumerType">The type of the consumer handling the message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="retryProcessFailedMessageInSecondsUnit">The time unit in seconds for retrying failed message processing.</param>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task UpdateExistingInboxFailedMessageAsync<TMessage>(
        IServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        TMessage message,
        Type consumerType,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        try
        {
            loggerFactory()
                .LogError(
                    exception.BeautifyStackTrace(),
                    "UpdateExistingInboxFailedMessageAsync. [[Error:{Error}]]; [[MessageType: {MessageType}]]; [[ConsumerType: {ConsumerType}]]; [[InboxJsonMessage: {InboxJsonMessage}]];",
                    exception.Message,
                    message.GetType().GetNameOrGenericTypeName(),
                    consumerType?.GetNameOrGenericTypeName() ?? "n/a",
                    existingInboxMessage.JsonMessage);

            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await serviceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                        {
                            // Retrieve the latest version of the inbox message to prevent concurrency issues.
                            var latestCurrentExistingInboxMessage =
                                await inboxBusMessageRepo.FirstOrDefaultAsync(
                                    p => p.Id == existingInboxMessage.Id &&
                                         p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processing,
                                    cancellationToken);

                            if (latestCurrentExistingInboxMessage != null)
                                await UpdateExistingInboxFailedMessageAsync(
                                    exception,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    latestCurrentExistingInboxMessage,
                                    inboxBusMessageRepo);
                        });
                },
                sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelaySeconds.Seconds(),
                retryCount: DefaultResilientRetiredCount,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex.BeautifyStackTrace(),
                    "UpdateExistingInboxFailedMessageAsync failed. [[Error:{Error}]]; [[MessageType: {MessageType}]]; [[ConsumerType: {ConsumerType}]]; [[InboxJsonMessage: {InboxJsonMessage}]];",
                    ex.Message,
                    existingInboxMessage.MessageTypeFullName,
                    existingInboxMessage.ConsumerBy,
                    existingInboxMessage.JsonMessage);
        }
    }

    private static async Task UpdateExistingInboxFailedMessageAsync(
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformInboxBusMessageRepository inboxBusMessageRepo)
    {
        existingInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Failed;
        existingInboxMessage.LastConsumeDate = Clock.UtcNow;
        existingInboxMessage.LastProcessingPingDate = Clock.UtcNow;
        existingInboxMessage.LastConsumeError = exception.BeautifyStackTrace().Serialize();
        existingInboxMessage.RetriedProcessCount = (existingInboxMessage.RetriedProcessCount ?? 0) + 1;
        existingInboxMessage.NextRetryProcessAfter = PlatformInboxBusMessage.CalculateNextRetryProcessAfter(
            existingInboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
    }
}
