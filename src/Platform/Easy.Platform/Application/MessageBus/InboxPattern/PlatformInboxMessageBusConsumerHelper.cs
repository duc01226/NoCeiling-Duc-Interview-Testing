using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public static class PlatformInboxMessageBusConsumerHelper
{
    public const int DefaultResilientRetiredCount = 5;
    public const int DefaultResilientRetiredDelayMilliseconds = 200;

    /// <summary>
    /// Inbox consumer support inbox pattern to prevent duplicated consumer message many times
    /// when event bus requeue message.
    /// This will stored consumed message into db. If message existed, it won't process the consumer.
    /// </summary>
    public static async Task HandleExecutingInboxConsumerAsync<TMessage>(
        IServiceProvider serviceProvider,
        IPlatformApplicationMessageBusConsumer<TMessage> consumer,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        PlatformInboxConfig inboxConfig,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        bool allowProcessInBackgroundThread,
        PlatformInboxBusMessage handleExistingInboxMessage,
        IUnitOfWork handleInUow,
        bool autoDeleteProcessedMessage = false,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (handleExistingInboxMessage != null && handleExistingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
        {
            await ExecuteDirectlyConsumerWithExistingInboxMessage(
                handleExistingInboxMessage,
                consumer,
                serviceProvider,
                message,
                routingKey,
                loggerFactory,
                retryProcessFailedMessageInSecondsUnit,
                autoDeleteProcessedMessage,
                cancellationToken);
        }
        else if (handleExistingInboxMessage == null)
        {
            var trackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

            var existedInboxMessage = trackId != null
                ? await inboxBusMessageRepository.FirstOrDefaultAsync(
                    p => p.Id == PlatformInboxBusMessage.BuildId(trackId, consumer.GetType()),
                    cancellationToken)
                : null;

            var newInboxMessage = existedInboxMessage == null
                ? await CreateNewInboxMessageAsync(
                    inboxBusMessageRepository,
                    consumer.GetType(),
                    message,
                    routingKey,
                    PlatformInboxBusMessage.ConsumeStatuses.Processing,
                    loggerFactory,
                    cancellationToken)
                : null;

            var toProcessInboxMessage = existedInboxMessage ?? newInboxMessage;

            if (existedInboxMessage == null ||
                PlatformInboxBusMessage.CanHandleMessagesExpr(inboxConfig.MessageProcessingMaxiSeconds).Compile()(existedInboxMessage))
            {
                if (handleInUow != null)
                {
                    handleInUow.OnCompletedActions.Add(
                        async () => await ExecuteConsumerForNewInboxMessage(
                            consumer.GetType(),
                            message,
                            toProcessInboxMessage,
                            routingKey,
                            autoDeleteProcessedMessage,
                            loggerFactory));
                }
                else
                {
                    // Check try CompleteAsync current active uow if any to ensure that newInboxMessage will be saved
                    // Do this to fix if someone open uow without complete it for some legacy project
                    if (inboxBusMessageRepository.UowManager().TryGetCurrentActiveUow() != null)
                        await inboxBusMessageRepository.UowManager().CurrentActiveUow().CompleteAsync(cancellationToken);

                    if (allowProcessInBackgroundThread || toProcessInboxMessage == existedInboxMessage)
                        Util.TaskRunner.QueueActionInBackground(
                            () => ExecuteConsumerForNewInboxMessage(
                                consumer.GetType(),
                                message,
                                toProcessInboxMessage,
                                routingKey,
                                autoDeleteProcessedMessage,
                                loggerFactory),
                            loggerFactory,
                            cancellationToken: cancellationToken);
                    else
                        await ExecuteConsumerForNewInboxMessage(
                            consumer.GetType(),
                            message,
                            toProcessInboxMessage,
                            routingKey,
                            autoDeleteProcessedMessage,
                            loggerFactory);
                }
            }
        }
    }

    public static async Task ExecuteConsumerForNewInboxMessage<TMessage>(
        Type consumerType,
        TMessage message,
        PlatformInboxBusMessage newInboxMessage,
        string routingKey,
        bool autoDeleteProcessedMessage,
        Func<ILogger> loggerFactory) where TMessage : class, new()
    {
        await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
            DoExecuteConsumerForNewInboxMessage,
            consumerType,
            message,
            newInboxMessage,
            routingKey,
            autoDeleteProcessedMessage,
            loggerFactory);

        static async Task DoExecuteConsumerForNewInboxMessage(
            Type consumerType,
            TMessage message,
            PlatformInboxBusMessage newInboxMessage,
            string routingKey,
            bool autoDeleteProcessedMessage,
            Func<ILogger> loggerFactory,
            IServiceProvider serviceProvider)
        {
            try
            {
                await serviceProvider.GetService(consumerType)
                    .Cast<IPlatformApplicationMessageBusConsumer<TMessage>>()
                    .With(_ => _.HandleDirectlyExistingInboxMessage = newInboxMessage)
                    .With(_ => _.AutoDeleteProcessedInboxEventMessage = autoDeleteProcessedMessage)
                    .HandleAsync(message, routingKey);
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
        }
    }

    public static async Task ExecuteDirectlyConsumerWithExistingInboxMessage<TMessage>(
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformApplicationMessageBusConsumer<TMessage> consumer,
        IServiceProvider serviceProvider,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        bool autoDeleteProcessedMessage,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        try
        {
            await consumer
                .With(_ => _.IsInstanceExecutingFromInboxHelper = true)
                .With(_ => _.AutoDeleteProcessedInboxEventMessage = autoDeleteProcessedMessage)
                .HandleAsync(message, routingKey);

            try
            {
                if (autoDeleteProcessedMessage)
                    await DeleteExistingInboxProcessedMessageAsync(
                        serviceProvider,
                        existingInboxMessage,
                        cancellationToken);
                else
                    await UpdateExistingInboxProcessedMessageAsync(
                        serviceProvider,
                        existingInboxMessage,
                        cancellationToken);
            }
            catch (Exception)
            {
                // If failed for some reason like concurrency token conflict or entity is not existing, try to update again by Id
                await UpdateExistingInboxProcessedMessageAsync(
                    serviceProvider,
                    existingInboxMessage.Id,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex,
                    "ExecuteConsumerForExistingInboxMessage failed. [[Error:{Error}]]; [MessageType: {MessageType}]; [ConsumerType: {ConsumerType}]; [RoutingKey: {RoutingKey}]; [MessageContent: {MessageContent}];",
                    ex.Message,
                    message.GetType().GetNameOrGenericTypeName(),
                    consumer.GetType().GetNameOrGenericTypeName(),
                    routingKey,
                    message.ToJson());

            await UpdateExistingInboxFailedMessageAsync(
                serviceProvider,
                existingInboxMessage,
                ex,
                retryProcessFailedMessageInSecondsUnit,
                loggerFactory,
                cancellationToken);
        }
    }

    public static async Task<PlatformInboxBusMessage> CreateNewInboxMessageAsync<TMessage>(
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        Type consumerType,
        TMessage message,
        string routingKey,
        PlatformInboxBusMessage.ConsumeStatuses consumeStatus,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        var newInboxMessage = PlatformInboxBusMessage.Create(
            message,
            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
            message.As<IPlatformTrackableBusMessage>()?.ProduceFrom,
            routingKey,
            consumerType,
            consumeStatus);

        try
        {
            return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    var result = await inboxBusMessageRepository.CreateAsync(
                        newInboxMessage,
                        dismissSendEvent: true,
                        sendEntityEventConfigure: null,
                        cancellationToken);

                    return result;
                },
                sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                retryCount: DefaultResilientRetiredCount);
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex,
                    "CreateNewInboxMessageAsync failed. [[Error:{Error}]], [RoutingKey:{RoutingKey}], [Type:{MessageType}]. NewInboxMessage: {NewInboxMessage}.",
                    ex.Message,
                    routingKey,
                    message.GetType().GetNameOrGenericTypeName(),
                    newInboxMessage.ToJson());
            throw;
        }
    }

    public static async Task UpdateExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        string existingInboxMessageId,
        CancellationToken cancellationToken = default)
    {
        await serviceProvider.ExecuteInjectScopedAsync(
            async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                var existingInboxMessage = await inboxBusMessageRepo.FirstOrDefaultAsync(
                    predicate: p => p.Id == existingInboxMessageId,
                    cancellationToken: cancellationToken);

                if (existingInboxMessage != null)
                    await UpdateExistingInboxProcessedMessageAsync(
                        serviceProvider,
                        existingInboxMessage,
                        cancellationToken);
            });
    }

    public static async Task UpdateExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        CancellationToken cancellationToken = default)
    {
        await serviceProvider.ExecuteInjectScopedAsync(
            async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                var toUpdateInboxMessage = existingInboxMessage
                    .With(_ => _.LastConsumeDate = DateTime.UtcNow)
                    .With(_ => _.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processed);

                await inboxBusMessageRepo.UpdateAsync(toUpdateInboxMessage, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);
            });
    }

    public static async Task DeleteExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        CancellationToken cancellationToken = default)
    {
        await serviceProvider.ExecuteInjectScopedAsync(
            async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                await inboxBusMessageRepo.DeleteAsync(existingInboxMessage.Id, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);
            });
    }

    public static async Task UpdateExistingInboxFailedMessageAsync(
        IServiceProvider serviceProvider,
        PlatformInboxBusMessage existingInboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        Func<ILogger> loggerFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await serviceProvider.ExecuteInjectScopedAsync(
                async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                {
                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        async () =>
                        {
                            // Get again to update to prevent concurrency error ensure that update messaged failed should not be failed
                            existingInboxMessage = await inboxBusMessageRepo.GetByIdAsync(existingInboxMessage.Id);

                            existingInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Failed;
                            existingInboxMessage.LastConsumeDate = DateTime.UtcNow;
                            existingInboxMessage.LastConsumeError = PlatformJsonSerializer.Serialize(new { exception.Message, exception.StackTrace });
                            existingInboxMessage.RetriedProcessCount = (existingInboxMessage.RetriedProcessCount ?? 0) + 1;
                            existingInboxMessage.NextRetryProcessAfter = PlatformInboxBusMessage.CalculateNextRetryProcessAfter(
                                existingInboxMessage.RetriedProcessCount,
                                retryProcessFailedMessageInSecondsUnit);

                            await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);
                        },
                        sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                        retryCount: DefaultResilientRetiredCount);
                });
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

    public static async Task UpdateFailedInboxMessageAsync(
        IServiceProvider serviceProvider,
        string id,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken = default)
    {
        await serviceProvider.ExecuteInjectScopedAsync(
            async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () =>
                    {
                        var existingInboxMessage = await inboxBusMessageRepo.GetByIdAsync(id, cancellationToken);
                        var consumeError = PlatformJsonSerializer.Serialize(
                            new
                            {
                                exception.Message,
                                exception.StackTrace
                            });

                        existingInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Failed;
                        existingInboxMessage.LastConsumeDate = DateTime.UtcNow;
                        existingInboxMessage.LastConsumeError = consumeError;
                        existingInboxMessage.RetriedProcessCount = (existingInboxMessage.RetriedProcessCount ?? 0) + 1;
                        existingInboxMessage.NextRetryProcessAfter = PlatformInboxBusMessage.CalculateNextRetryProcessAfter(
                            retriedProcessCount: existingInboxMessage.RetriedProcessCount,
                            retryProcessFailedMessageInSecondsUnit);

                        await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);
                    },
                    sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                    retryCount: DefaultResilientRetiredCount);
            });
    }
}
