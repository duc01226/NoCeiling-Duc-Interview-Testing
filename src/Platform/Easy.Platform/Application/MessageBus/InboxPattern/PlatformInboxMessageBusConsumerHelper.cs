using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
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
    public static async Task HandleExecutingInboxConsumerInternalHandleAsync<TMessage>(
        IServiceProvider serviceProvider,
        IPlatformMessageBusConsumer<TMessage> consumer,
        IPlatformInboxBusMessageRepository inboxBusMessageRepo,
        Func<TMessage, string, Task> internalHandleAsync,
        TMessage message,
        string routingKey,
        ILogger logger,
        double retryProcessFailedMessageInSecondsUnit,
        string handleExistingInboxMessageTrackId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        var trackId = handleExistingInboxMessageTrackId ?? message.As<IPlatformTrackableBusMessage>()?.TrackingId;

        var existingInboxMessage = trackId != null
            ? await inboxBusMessageRepo.FirstOrDefaultAsync(
                p => p.Id == PlatformInboxBusMessage.BuildId(trackId, GetConsumerByValue(consumer)),
                cancellationToken)
            : null;

        if (existingInboxMessage != null)
        {
            await HandleExistingInboxMessage(
                existingInboxMessage,
                serviceProvider,
                internalHandleAsync,
                message,
                routingKey,
                logger,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
        else
        {
            var newInboxMessage = await CreateNewInboxMessageAsync(
                serviceProvider,
                consumer,
                message,
                routingKey,
                PlatformInboxBusMessage.ConsumeStatuses.Processing,
                cancellationToken);

            await TriggerHandleWaitingProcessingInboxMessageConsumer(consumer, message, newInboxMessage, routingKey, logger);
        }
    }

    public static Task TriggerHandleWaitingProcessingInboxMessageConsumer<TMessage>(
        IPlatformMessageBusConsumer<TMessage> consumer,
        TMessage message,
        PlatformInboxBusMessage newInboxMessage,
        string routingKey,
        ILogger logger) where TMessage : class, new()
    {
        var consumerType = consumer.GetType();

        return PlatformApplicationGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
            async (IServiceProvider serviceProvider) =>
            {
                try
                {
                    await serviceProvider.GetService(consumerType)
                        .Cast<IPlatformMessageBusConsumer<TMessage>>()
                        .With(_ => _.HandleExistingInboxMessageTrackId = newInboxMessage.GetTrackId())
                        .HandleAsync(message, routingKey);
                }
                catch (Exception e)
                {
                    // Catch and just log error to prevent retry queue message. Inbox message will be automatically retry handling via
                    // inbox hosted service
                    logger.LogError(
                        e,
                        "TriggerHandleWaitingProcessingInboxMessageConsumer [ConsumerType:{consumerType}] failed. InboxMessage will be retried later.",
                        consumerType.Name);
                }
            });
    }

    public static async Task HandleExistingInboxMessage<TMessage>(
        PlatformInboxBusMessage existingInboxMessage,
        IServiceProvider serviceProvider,
        Func<TMessage, string, Task> internalHandleAsync,
        TMessage message,
        string routingKey,
        ILogger logger,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        if (existingInboxMessage.ConsumeStatus != PlatformInboxBusMessage.ConsumeStatuses.Processed)
            try
            {
                await internalHandleAsync(message, routingKey);

                await UpdateExistingInboxProcessedMessageAsync(
                    serviceProvider,
                    existingInboxMessage.Id,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    $"Error Consume inbox message [RoutingKey:{{RoutingKey}}], [Type:{{MessageType}}].{Environment.NewLine}" +
                    "Message Info: {BusMessage}.",
                    routingKey,
                    message.GetType().GetNameOrGenericTypeName(),
                    message.AsJson());

                await UpdateExistingInboxFailedMessageAsync(
                    serviceProvider,
                    existingInboxMessage.Id,
                    ex,
                    retryProcessFailedMessageInSecondsUnit,
                    logger,
                    cancellationToken);
            }
    }

    public static string GetConsumerByValue<TMessage>(IPlatformMessageBusConsumer<TMessage> consumer)
        where TMessage : class, new()
    {
        return GetConsumerByValue(consumer.GetType());
    }

    public static string GetConsumerByValue(Type consumerType)
    {
        return consumerType.FullName;
    }

    public static async Task<PlatformInboxBusMessage> CreateNewInboxMessageAsync<TMessage>(
        IServiceProvider serviceProvider,
        IPlatformMessageBusConsumer<TMessage> consumer,
        TMessage message,
        string routingKey,
        PlatformInboxBusMessage.ConsumeStatuses consumeStatus,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        return await serviceProvider.ExecuteInjectScopedAsync<PlatformInboxBusMessage>(
            async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                using (var uow = uowManager.Begin())
                {
                    var result = await inboxBusMessageRepo.CreateAsync(
                        PlatformInboxBusMessage.Create(
                            message,
                            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
                            message.As<IPlatformTrackableBusMessage>()?.ProduceFrom,
                            routingKey,
                            GetConsumerByValue(consumer),
                            consumeStatus),
                        dismissSendEvent: true,
                        cancellationToken);

                    await uow.CompleteAsync(cancellationToken);

                    return result;
                }
            });
    }

    public static async Task UpdateExistingInboxProcessedMessageAsync(
        IServiceProvider serviceProvider,
        string existingInboxMessageId,
        CancellationToken cancellationToken = default)
    {
        await serviceProvider.ExecuteInjectScopedAsync(
            async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxBusMessageRepo) =>
            {
                using (var uow = uowManager.Begin())
                {
                    var existingInboxMessage = await inboxBusMessageRepo.GetByIdAsync(existingInboxMessageId, cancellationToken);
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
