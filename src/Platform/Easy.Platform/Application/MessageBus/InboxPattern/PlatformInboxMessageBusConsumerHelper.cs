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
        PlatformInboxBusMessage handleImmediatelyExistingInboxMessage = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (handleImmediatelyExistingInboxMessage != null && handleImmediatelyExistingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
        {
            await ExecuteConsumerForExistingInboxMessage(
                handleImmediatelyExistingInboxMessage,
                consumer,
                serviceProvider,
                message,
                routingKey,
                loggerFactory,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
        else if (handleImmediatelyExistingInboxMessage == null)
        {
            var trackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

            var existingInboxMessage = trackId != null
                ? await inboxBusMessageRepository.FirstOrDefaultAsync(
                    p => p.Id == PlatformInboxBusMessage.BuildId(trackId, consumer.GetType()),
                    cancellationToken)
                : null;

            var newInboxMessage = existingInboxMessage == null
                ? await CreateNewInboxMessageAsync(
                    serviceProvider,
                    consumer.GetType(),
                    message,
                    routingKey,
                    PlatformInboxBusMessage.ConsumeStatuses.Processing,
                    loggerFactory,
                    cancellationToken)
                : null;

            var toProcessInboxMessage = existingInboxMessage ?? newInboxMessage;

            if (toProcessInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
                ExecuteConsumerForExistingInboxMessageInBackground(
                    consumer.GetType(),
                    message,
                    existingInboxMessage ?? newInboxMessage,
                    routingKey,
                    loggerFactory);
        }
    }

    public static void ExecuteConsumerForExistingInboxMessageInBackground<TMessage>(
        Type consumerType,
        TMessage message,
        PlatformInboxBusMessage existingInboxMessage,
        string routingKey,
        Func<ILogger> loggerFactory) where TMessage : class, new()
    {
        Util.TaskRunner.QueueActionInBackground(
            () => PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                DoTriggerHandleExistingInboxMessageInBackground,
                consumerType,
                message,
                existingInboxMessage,
                routingKey,
                loggerFactory),
            loggerFactory);

        static async Task DoTriggerHandleExistingInboxMessageInBackground(
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
                    .With(_ => _.HandleExistingInboxMessage = existingInboxMessage)
                    .HandleAsync(message, routingKey);
            }
            catch (Exception e)
            {
                // Catch and just log error to prevent retry queue message. Inbox message will be automatically retry handling via inbox hosted service
                loggerFactory()
                    .LogError(
                        e,
                        $"{nameof(ExecuteConsumerForExistingInboxMessageInBackground)} [Consumer:{{ConsumerType}}] failed. Inbox message will be automatically retry later.",
                        consumerType.Name);
            }
        }
    }

    public static async Task ExecuteConsumerForExistingInboxMessage<TMessage>(
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformApplicationMessageBusConsumer<TMessage> consumer,
        IServiceProvider serviceProvider,
        TMessage message,
        string routingKey,
        Func<ILogger> loggerFactory,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        if (existingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
            try
            {
                await consumer
                    .With(_ => _.IsInstanceExecutingFromInboxHelper = true)
                    .HandleAsync(message, routingKey);

                await UpdateExistingInboxProcessedMessageAsync(
                    serviceProvider,
                    existingInboxMessage,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                loggerFactory()
                    .LogError(
                        ex,
                        "Error Consume inbox message. [MessageType: {MessageType}]; [ConsumerType: {ConsumerType}]; [ErrorMessage: {ErrorMessage}]; [RoutingKey: {RoutingKey}]; [MessageContent: {MessageContent}];",
                        message.GetType().GetNameOrGenericTypeName(),
                        consumer.GetType().GetNameOrGenericTypeName(),
                        ex.Message,
                        routingKey,
                        message.ToJson());

                await UpdateExistingInboxFailedMessageAsync(
                    serviceProvider,
                    existingInboxMessage.Id,
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
                    var result = await inboxBusMessageRepo.CreateAsync(
                        newInboxMessage,
                        dismissSendEvent: true,
                        cancellationToken);

                    return result;
                });
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex,
                    "Error Create inbox message [RoutingKey:{RoutingKey}], [Type:{MessageType}]. NewInboxMessage: {NewInboxMessage}.",
                    routingKey,
                    message.GetType().GetNameOrGenericTypeName(),
                    newInboxMessage.ToJson());
            throw;
        }
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

                await inboxBusMessageRepo.UpdateAsync(toUpdateInboxMessage, dismissSendEvent: true, cancellationToken);
            });
    }

    public static async Task UpdateExistingInboxFailedMessageAsync(
        IServiceProvider serviceProvider,
        string existingInboxMessageId,
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
                    var existingInboxMessage = await inboxBusMessageRepo.GetByIdAsync(existingInboxMessageId, cancellationToken);

                    existingInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Failed;
                    existingInboxMessage.LastConsumeDate = DateTime.UtcNow;
                    existingInboxMessage.LastConsumeError = PlatformJsonSerializer.Serialize(new { exception.Message, exception.StackTrace });
                    existingInboxMessage.RetriedProcessCount = (existingInboxMessage.RetriedProcessCount ?? 0) + 1;
                    existingInboxMessage.NextRetryProcessAfter = PlatformInboxBusMessage.CalculateNextRetryProcessAfter(
                        existingInboxMessage.RetriedProcessCount,
                        retryProcessFailedMessageInSecondsUnit);

                    await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, cancellationToken);
                });
        }
        catch (Exception ex)
        {
            loggerFactory()
                .LogError(
                    ex,
                    "Error UpdateExistingInboxFailedMessageAsync message [MessageId:{ExistingInboxMessageId}].",
                    existingInboxMessageId);
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
            async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                using (var uow = uowManager.Begin())
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

                    await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, cancellationToken);

                    await uow.CompleteAsync(cancellationToken);
                }
            });
    }
}
