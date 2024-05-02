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

public static class PlatformInboxMessageBusConsumerHelper
{
    public const int DefaultResilientRetiredCount = 2;
    public const int DefaultResilientRetiredDelayMilliseconds = 200;

    /// <summary>
    /// Inbox consumer support inbox pattern to prevent duplicated consumer message many times
    /// when event bus requeue message.
    /// This will stored consumed message into db. If message existed, it won't process the consumer.
    /// </summary>
    public static async Task HandleExecutingInboxConsumerAsync<TMessage>(
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider,
        Type consumerType,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        PlatformInboxConfig inboxConfig,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        bool allowProcessInBackgroundThread,
        PlatformInboxBusMessage handleExistingInboxMessage,
        IPlatformApplicationMessageBusConsumer<TMessage> handleExistingInboxMessageConsumerInstance,
        IPlatformUnitOfWork handleInUow,
        string extendedMessageIdPrefix,
        bool autoDeleteProcessedMessageImmediately = false,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (handleExistingInboxMessage != null && handleExistingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
            await HandleConsumerLogicDirectlyForExistingInboxMessage(
                handleExistingInboxMessage,
                handleExistingInboxMessageConsumerInstance,
                inboxConfig,
                serviceProvider,
                inboxBusMessageRepository,
                message,
                routingKey,
                loggerFactory,
                retryProcessFailedMessageInSecondsUnit,
                autoDeleteProcessedMessageImmediately,
                cancellationToken);
        else if (handleExistingInboxMessage == null)
            await SaveAndTryConsumeNewInboxMessageAsync(
                rootServiceProvider,
                consumerType,
                inboxBusMessageRepository,
                inboxConfig,
                message,
                routingKey,
                loggerFactory,
                allowProcessInBackgroundThread,
                handleInUow,
                autoDeleteProcessedMessageImmediately,
                extendedMessageIdPrefix,
                cancellationToken);
    }

    private static async Task SaveAndTryConsumeNewInboxMessageAsync<TMessage>(
        IPlatformRootServiceProvider rootServiceProvider,
        Type consumerType,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        PlatformInboxConfig inboxConfig,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        bool allowProcessInBackgroundThread,
        IPlatformUnitOfWork handleInUow,
        bool autoDeleteProcessedMessage,
        string extendedMessageIdPrefix,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        var (toProcessInboxMessage, existedInboxMessage) =
            await GetOrCreateToProcessInboxMessage(consumerType, inboxBusMessageRepository, message, routingKey, extendedMessageIdPrefix, cancellationToken);

        if (toProcessInboxMessage == null) return;

        if (existedInboxMessage == null ||
            PlatformInboxBusMessage.CanHandleMessagesExpr(inboxConfig.MessageProcessingMaxSeconds).Compile()(existedInboxMessage))
        {
            if (handleInUow != null && !handleInUow.IsPseudoTransactionUow())
            {
                handleInUow.OnSaveChangesCompletedActions.Add(
                    async () => await ExecuteConsumerForNewInboxMessage(
                        rootServiceProvider,
                        consumerType,
                        inboxConfig,
                        message,
                        toProcessInboxMessage,
                        routingKey,
                        autoDeleteProcessedMessage,
                        loggerFactory));
            }
            else
            {
                // Check try CompleteAsync current active uow if any to ensure that newInboxMessage will be saved
                // Do this to fix if someone open uow without complete/save it for some legacy project
                if (inboxBusMessageRepository.UowManager().TryGetCurrentActiveUow() != null)
                    await inboxBusMessageRepository.UowManager().CurrentActiveUow().SaveChangesAsync(cancellationToken);

                if (allowProcessInBackgroundThread || toProcessInboxMessage == existedInboxMessage)
                    Util.TaskRunner.QueueActionInBackground(
                        () => ExecuteConsumerForNewInboxMessage(
                            rootServiceProvider,
                            consumerType,
                            inboxConfig,
                            message,
                            toProcessInboxMessage,
                            routingKey,
                            autoDeleteProcessedMessage,
                            loggerFactory),
                        loggerFactory,
                        cancellationToken: cancellationToken);
                else
                    await ExecuteConsumerForNewInboxMessage(
                        rootServiceProvider,
                        consumerType,
                        inboxConfig,
                        message,
                        toProcessInboxMessage,
                        routingKey,
                        autoDeleteProcessedMessage,
                        loggerFactory);
            }
        }
    }

    private static async Task<(PlatformInboxBusMessage, PlatformInboxBusMessage)> GetOrCreateToProcessInboxMessage<TMessage>(
        Type consumerType,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        TMessage message,
        string routingKey,
        string extendedMessageIdPrefix,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var trackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

                var existedInboxMessage = trackId != null
                    ? await inboxBusMessageRepository.FirstOrDefaultAsync(
                        p => p.Id == PlatformInboxBusMessage.BuildId(consumerType, trackId, extendedMessageIdPrefix),
                        cancellationToken)
                    : null;
                var isAnySameConsumerOtherNotProcessedMessage = await inboxBusMessageRepository.AnyAsync(
                    PlatformInboxBusMessage.CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(
                        consumerType,
                        trackId,
                        existedInboxMessage?.CreatedDate ?? Clock.UtcNow,
                        extendedMessageIdPrefix),
                    cancellationToken);

                var newInboxMessage = existedInboxMessage == null
                    ? await CreateNewInboxMessageAsync(
                        inboxBusMessageRepository,
                        consumerType,
                        message,
                        routingKey,
                        isAnySameConsumerOtherNotProcessedMessage ? PlatformInboxBusMessage.ConsumeStatuses.New : PlatformInboxBusMessage.ConsumeStatuses.Processing,
                        extendedMessageIdPrefix,
                        cancellationToken)
                    : null;

                var toProcessInboxMessage = isAnySameConsumerOtherNotProcessedMessage
                    ? null
                    : existedInboxMessage ?? newInboxMessage;

                return (toProcessInboxMessage, existedInboxMessage);
            },
            _ => DefaultResilientRetiredDelayMilliseconds.Milliseconds(),
            DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    public static async Task ExecuteConsumerForNewInboxMessage<TMessage>(
        IPlatformRootServiceProvider rootServiceProvider,
        Type consumerType,
        PlatformInboxConfig inboxConfig,
        TMessage message,
        PlatformInboxBusMessage newInboxMessage,
        string routingKey,
        bool autoDeleteProcessedMessage,
        Func<ILogger> loggerFactory) where TMessage : class, new()
    {
        await rootServiceProvider.ExecuteInjectScopedAsync(
            async (IServiceProvider serviceProvider) =>
            {
                try
                {
                    var consumer = serviceProvider.GetService(consumerType)
                        .Cast<IPlatformApplicationMessageBusConsumer<TMessage>>()
                        .With(_ => _.HandleExistingInboxMessage = newInboxMessage)
                        .With(_ => _.AutoDeleteProcessedInboxEventMessageImmediately = autoDeleteProcessedMessage);

                    await consumer
                        .HandleAsync(message, routingKey)
                        .Timeout(consumer.InboxProcessingMaxTimeout ?? inboxConfig.MessageProcessingMaxSeconds.Seconds());
                }
                catch (Exception e)
                {
                    // Catch and just log error to prevent retry queue message. Inbox message will be automatically retry handling via inbox hosted service
                    loggerFactory()
                        .LogError(
                            e,
                            $"{nameof(ExecuteConsumerForNewInboxMessage)} [Consumer:{{ConsumerType}}] failed. Inbox message will be automatically retry later.",
                            consumerType.Name);
                }
            });
    }

    public static async Task HandleConsumerLogicDirectlyForExistingInboxMessage<TMessage>(
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformApplicationMessageBusConsumer<TMessage> consumer,
        PlatformInboxConfig inboxConfig,
        IServiceProvider serviceProvider,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        bool autoDeleteProcessedMessage,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        try
        {
            if (await inboxBusMessageRepository.AnyAsync(
                PlatformInboxBusMessage.CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(existingInboxMessage),
                cancellationToken))
                await RevertExistingInboxToNewMessageAsync(existingInboxMessage, inboxBusMessageRepository, cancellationToken);
            else
                await consumer
                    .With(_ => _.IsHandlingLogicForInboxMessage = true)
                    .With(_ => _.AutoDeleteProcessedInboxEventMessageImmediately = autoDeleteProcessedMessage)
                    .HandleAsync(message, routingKey)
                    .Timeout(consumer.InboxProcessingMaxTimeout ?? inboxConfig.MessageProcessingMaxSeconds.Seconds());

            try
            {
                await UpdateExistingInboxProcessedMessageAsync(
                    serviceProvider.GetRequiredService<IPlatformRootServiceProvider>(),
                    existingInboxMessage,
                    cancellationToken);
            }
            catch (Exception)
            {
                // If failed for some reason like concurrency token conflict or entity is not existing, try to update again by Id
                await UpdateExistingInboxProcessedMessageAsync(
                    serviceProvider,
                    existingInboxMessage.Id,
                    loggerFactory,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await UpdateExistingInboxFailedMessageAsync(
                serviceProvider,
                existingInboxMessage,
                message,
                consumer.GetType(),
                routingKey,
                ex,
                retryProcessFailedMessageInSecondsUnit,
                loggerFactory,
                cancellationToken);
        }
        finally
        {
            if (autoDeleteProcessedMessage)
                await DeleteExistingInboxProcessedMessageAsync(
                    serviceProvider,
                    existingInboxMessage,
                    loggerFactory,
                    cancellationToken);
        }
    }

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
            sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    public static async Task<PlatformInboxBusMessage> CreateNewInboxMessageAsync<TMessage>(
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        Type consumerType,
        TMessage message,
        string routingKey,
        PlatformInboxBusMessage.ConsumeStatuses consumeStatus,
        string extendedMessageIdPrefix,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        var newInboxMessage = PlatformInboxBusMessage.Create(
            message,
            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
            message.As<IPlatformTrackableBusMessage>()?.ProduceFrom,
            routingKey,
            consumerType,
            consumeStatus,
            extendedMessageIdPrefix,
            null);

        var result = await inboxBusMessageRepository.CreateImmediatelyAsync(
            newInboxMessage,
            dismissSendEvent: true,
            eventCustomConfig: null,
            cancellationToken);

        return result;
    }

    public static async Task UpdateExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        string existingInboxMessageId,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
                });
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex,
                    "UpdateExistingInboxProcessedMessageAsync failed. [[Error:{Error}]], [ExistingInboxMessageId:{ExistingInboxMessageId}].",
                    ex.Message,
                    existingInboxMessageId);
        }
    }

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
                            toUpdateInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processed;

                            await inboxBusMessageRepo.UpdateAsync(toUpdateInboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
                        }
                        catch (PlatformDomainRowVersionConflictException)
                        {
                            toUpdateInboxMessage = await serviceProvider.ExecuteInjectScopedAsync<PlatformInboxBusMessage>(
                                (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                                    inboxBusMessageRepo.GetByIdAsync(toUpdateInboxMessage.Id, cancellationToken));
                            throw;
                        }
                    });
            },
            sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

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
                            await inboxBusMessageRepo.DeleteAsync(existingInboxMessage.Id, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);

                            await inboxBusMessageRepo.DeleteManyAsync(
                                predicate: p => p.Id == existingInboxMessage.Id && p.ConsumeStatus == PlatformInboxBusMessage.ConsumeStatuses.Processed,
                                dismissSendEvent: true,
                                eventCustomConfig: null,
                                cancellationToken);
                        });
                },
                sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                retryCount: DefaultResilientRetiredCount,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            loggerFactory().LogError(e, "Try DeleteExistingInboxProcessedMessageAsync failed");
        }
    }

    public static async Task UpdateExistingInboxFailedMessageAsync<TMessage>(
        IServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        TMessage message,
        Type consumerType,
        string routingKey,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        try
        {
            loggerFactory()
                .LogError(
                    exception,
                    "UpdateExistingInboxFailedMessageAsync. [[Error:{Error}]]; [MessageType: {MessageType}]; [ConsumerType: {ConsumerType}]; [RoutingKey: {RoutingKey}]; [MessageContent: {MessageContent}];",
                    exception.Message,
                    message.GetType().GetNameOrGenericTypeName(),
                    consumerType?.GetNameOrGenericTypeName() ?? "n/a",
                    routingKey,
                    message.ToJson());

            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await serviceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                        {
                            // Get again to update to prevent concurrency error ensure that update messaged failed should not be failed
                            var latestCurrentExistingInboxMessage =
                                await inboxBusMessageRepo.FirstOrDefaultAsync(p => p.Id == existingInboxMessage.Id, cancellationToken);

                            if (latestCurrentExistingInboxMessage != null)
                                await UpdateExistingInboxFailedMessageAsync(
                                    exception,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    latestCurrentExistingInboxMessage,
                                    inboxBusMessageRepo);
                        });
                },
                sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                retryCount: DefaultResilientRetiredCount,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex,
                    "UpdateExistingInboxFailedMessageAsync failed. [[Error:{Error}]]. [InboxMessage:{Message}].",
                    ex.Message,
                    existingInboxMessage.ToJson());
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
        existingInboxMessage.LastConsumeError = exception.Serialize();
        existingInboxMessage.RetriedProcessCount = (existingInboxMessage.RetriedProcessCount ?? 0) + 1;
        existingInboxMessage.NextRetryProcessAfter = PlatformInboxBusMessage.CalculateNextRetryProcessAfter(
            existingInboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
    }
}
