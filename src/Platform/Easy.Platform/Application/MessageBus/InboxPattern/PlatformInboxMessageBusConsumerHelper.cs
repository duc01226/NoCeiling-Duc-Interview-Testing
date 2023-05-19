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
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        bool allowProcessInBackgroundThread,
        PlatformInboxBusMessage handleDirectlyExistingInboxMessage = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (handleDirectlyExistingInboxMessage != null && handleDirectlyExistingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
        {
            await ExecuteDirectlyConsumerWithExistingInboxMessage(
                handleDirectlyExistingInboxMessage,
                consumer,
                serviceProvider,
                message,
                routingKey,
                loggerFactory,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
        else if (handleDirectlyExistingInboxMessage == null)
        {
            var trackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

            var existedInboxMessage = trackId != null
                ? await inboxBusMessageRepository.FirstOrDefaultAsync(
                    p => p.Id == PlatformInboxBusMessage.BuildId(trackId, consumer.GetType()),
                    cancellationToken)
                : null;

            var newInboxMessage = existedInboxMessage == null
                ? await CreateNewInboxMessageAsync(
                    serviceProvider,
                    consumer.GetType(),
                    message,
                    routingKey,
                    PlatformInboxBusMessage.ConsumeStatuses.Processing,
                    loggerFactory,
                    cancellationToken)
                : null;

            var toProcessInboxMessage = existedInboxMessage ?? newInboxMessage;

            if (toProcessInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
            {
                if (allowProcessInBackgroundThread || toProcessInboxMessage == existedInboxMessage)
                    Util.TaskRunner.QueueActionInBackground(
                        () => ExecuteConsumerForNewInboxMessage(
                            consumer.GetType(),
                            message,
                            toProcessInboxMessage,
                            routingKey,
                            loggerFactory),
                        loggerFactory,
                        cancellationToken: cancellationToken);
                else
                    await ExecuteConsumerForNewInboxMessage(
                        consumer.GetType(),
                        message,
                        toProcessInboxMessage,
                        routingKey,
                        loggerFactory);
            }
        }
    }

    public static async Task ExecuteConsumerForNewInboxMessage<TMessage>(
        Type consumerType,
        TMessage message,
        PlatformInboxBusMessage existingInboxMessage,
        string routingKey,
        Func<ILogger> loggerFactory) where TMessage : class, new()
    {
        await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
            DoExecuteConsumerForNewInboxMessage,
            consumerType,
            message,
            existingInboxMessage,
            routingKey,
            loggerFactory);

        static async Task DoExecuteConsumerForNewInboxMessage(
            Type consumerType,
            TMessage message,
            PlatformInboxBusMessage existingInboxMessage,
            string routingKey,
            Func<ILogger> loggerFactory,
            IServiceProvider serviceProvider)
        {
            try
            {
                await serviceProvider.GetService(consumerType)
                    .Cast<IPlatformApplicationMessageBusConsumer<TMessage>>()
                    .With(_ => _.HandleDirectlyExistingInboxMessage = existingInboxMessage)
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
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        try
        {
            await consumer
                .With(_ => _.IsInstanceExecutingFromInboxHelper = true)
                .HandleAsync(message, routingKey);
            try
            {
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
                    "ExecuteConsumerForExistingInboxMessage failed. [MessageType: {MessageType}]; [ConsumerType: {ConsumerType}]; [RoutingKey: {RoutingKey}]; [MessageContent: {MessageContent}];",
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
        IServiceProvider serviceProvider,
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
            return await serviceProvider.ExecuteInjectScopedAsync<PlatformInboxBusMessage>(
                async (IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                {
                    return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        async () =>
                        {
                            var result = await inboxBusMessageRepo.CreateAsync(
                                newInboxMessage,
                                dismissSendEvent: true,
                                sendEntityEventConfigure: null,
                                cancellationToken);

                            return result;
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
                    "CreateNewInboxMessageAsync failed. [RoutingKey:{RoutingKey}], [Type:{MessageType}]. NewInboxMessage: {NewInboxMessage}.",
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
                var existingInboxMessage = await inboxBusMessageRepo.FirstOrDefaultAsync(predicate: p => p.Id == existingInboxMessageId);

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
            async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                var toUpdateInboxMessage = existingInboxMessage
                    .With(_ => _.LastConsumeDate = DateTime.UtcNow)
                    .With(_ => _.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processed);

                await inboxBusMessageRepo.UpdateAsync(toUpdateInboxMessage, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);
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
                    "UpdateExistingInboxFailedMessageAsync failed. [InboxMessage:{Message}].",
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
