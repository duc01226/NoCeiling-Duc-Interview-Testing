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
        IPlatformMessageBusConsumer<TMessage> consumer,
        IPlatformInboxBusMessageRepository inboxBusMessageRepository,
        TMessage message,
        string routingKey,
        ILogger logger,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformInboxBusMessage handleImmediatelyExistingInboxMessage = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        var trackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

        var existingInboxMessage = trackId != null && handleImmediatelyExistingInboxMessage == null
            ? await inboxBusMessageRepository.FirstOrDefaultAsync(
                p => p.Id == PlatformInboxBusMessage.BuildId(trackId, consumer.GetType()),
                cancellationToken)
            : handleImmediatelyExistingInboxMessage;

        var newInboxMessage = existingInboxMessage == null
            ? await CreateNewInboxMessageAsync(
                serviceProvider,
                consumer,
                message,
                routingKey,
                PlatformInboxBusMessage.ConsumeStatuses.Processing,
                logger,
                cancellationToken)
            : null;

        if (handleImmediatelyExistingInboxMessage != null)
            await HandleExistingInboxMessage(
                existingInboxMessage,
                consumer,
                serviceProvider,
                message,
                routingKey,
                logger,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        else
            TriggerHandleExistingInboxMessageInBackground(
                consumer,
                message,
                existingInboxMessage ?? newInboxMessage,
                routingKey,
                logger);
    }

    public static void TriggerHandleExistingInboxMessageInBackground<TMessage>(
        IPlatformMessageBusConsumer<TMessage> consumer,
        TMessage message,
        PlatformInboxBusMessage existingInboxMessage,
        string routingKey,
        ILogger logger) where TMessage : class, new()
    {
        var consumerType = consumer.GetType();

        Util.TaskRunner.QueueActionInBackground(
            () =>
                PlatformApplicationGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                    async (IServiceProvider serviceProvider) =>
                    {
                        try
                        {
                            await serviceProvider.GetService(consumerType)
                                .Cast<IPlatformMessageBusConsumer<TMessage>>()
                                .With(_ => _.HandleExistingInboxMessage = existingInboxMessage)
                                .HandleAsync(message, routingKey);
                        }
                        catch (Exception e)
                        {
                            // Catch and just log error to prevent retry queue message. Inbox message will be automatically retry handling via inbox hosted service
                            logger.LogError(
                                e,
                                $"{nameof(TriggerHandleExistingInboxMessageInBackground)} [Consumer:{{ConsumerType}}] failed. Inbox message will be automatically retry later.",
                                consumerType.Name);
                        }
                    }),
            logger);
    }

    public static async Task HandleExistingInboxMessage<TMessage>(
        PlatformInboxBusMessage existingInboxMessage,
        IPlatformMessageBusConsumer<TMessage> consumer,
        IServiceProvider serviceProvider,
        TMessage message,
        string routingKey,
        ILogger logger,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        if (existingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
            try
            {
                await consumer.With(_ => _.HandleExistingInboxMessage = existingInboxMessage).HandleLogicAsync(message, routingKey);

                await UpdateExistingInboxProcessedMessageAsync(
                    serviceProvider,
                    existingInboxMessage,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error Consume inbox message [RoutingKey:{RoutingKey}], [Type:{MessageType}]. Message: {Message}.",
                    routingKey,
                    message.GetType().GetNameOrGenericTypeName(),
                    message.ToJson());

                await UpdateExistingInboxFailedMessageAsync(
                    serviceProvider,
                    existingInboxMessage.Id,
                    ex,
                    retryProcessFailedMessageInSecondsUnit,
                    logger,
                    cancellationToken);
            }
    }

    public static async Task<PlatformInboxBusMessage> CreateNewInboxMessageAsync<TMessage>(
        IServiceProvider serviceProvider,
        IPlatformMessageBusConsumer<TMessage> consumer,
        TMessage message,
        string routingKey,
        PlatformInboxBusMessage.ConsumeStatuses consumeStatus,
        ILogger logger,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        var newInboxMessage = PlatformInboxBusMessage.Create(
            message,
            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
            message.As<IPlatformTrackableBusMessage>()?.ProduceFrom,
            routingKey,
            consumer.GetType(),
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
            logger.LogError(
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
                using (var uow = uowManager.Begin())
                {
                    existingInboxMessage.LastConsumeDate = DateTime.UtcNow;
                    existingInboxMessage.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processed;

                    await inboxBusMessageRepo.UpdateAsync(existingInboxMessage, dismissSendEvent: true, cancellationToken);

                    await uow.CompleteAsync(cancellationToken);
                }
            });
    }

    public static async Task UpdateExistingInboxFailedMessageAsync(
        IServiceProvider serviceProvider,
        string existingInboxMessageId,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await serviceProvider.ExecuteInjectScopedAsync(
                async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
                {
                    using (var uow = uowManager.Begin())
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

                        await uow.CompleteAsync(cancellationToken);
                    }
                });
        }
        catch (Exception ex)
        {
            logger.LogError(
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
