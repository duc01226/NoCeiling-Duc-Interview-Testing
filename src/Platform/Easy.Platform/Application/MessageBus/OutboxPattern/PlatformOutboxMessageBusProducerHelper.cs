using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Easy.Platform.Persistence.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxMessageBusProducerHelper : IPlatformHelper
{
    private readonly IPlatformMessageBusProducer messageBusProducer;
    private readonly PlatformOutboxConfig outboxConfig;
    private readonly IServiceProvider serviceProvider;

    public PlatformOutboxMessageBusProducerHelper(
        PlatformOutboxConfig outboxConfig,
        IPlatformMessageBusProducer messageBusProducer,
        IServiceProvider serviceProvider)
    {
        this.outboxConfig = outboxConfig;
        this.messageBusProducer = messageBusProducer;
        this.serviceProvider = serviceProvider;
    }

    public async Task HandleSendingOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string handleExistingOutboxMessageId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (serviceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null)
        {
            if (outboxConfig.StandaloneScopeForOutbox)
                await serviceProvider.ExecuteInjectScopedAsync(
                    SendOutboxMessageAsync<TMessage>,
                    message,
                    routingKey,
                    retryProcessFailedMessageInSecondsUnit,
                    handleExistingOutboxMessageId,
                    cancellationToken);
            else
                await serviceProvider.ExecuteInjectAsync(
                    SendOutboxMessageAsync<TMessage>,
                    message,
                    routingKey,
                    retryProcessFailedMessageInSecondsUnit,
                    handleExistingOutboxMessageId,
                    cancellationToken);
        }
        else
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
        }
    }

    public static async Task SendOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string handleExistingOutboxMessageId,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        IPlatformMessageBusProducer messageBusProducer,
        ILogger logger,
        IServiceProvider serviceProvider,
        IUnitOfWorkManager unitOfWorkManager) where TMessage : class, new()
    {
        if (handleExistingOutboxMessageId != null)
        {
            var existingOutboxMessage = await outboxBusMessageRepository.GetByIdAsync(
                handleExistingOutboxMessageId,
                cancellationToken);

            if (existingOutboxMessage.SendStatus is
                PlatformOutboxBusMessage.SendStatuses.New
                or PlatformOutboxBusMessage.SendStatuses.Failed
                or PlatformOutboxBusMessage.SendStatuses.Processing)
                await SendExistingOutboxMessageAsync(
                    existingOutboxMessage,
                    message,
                    routingKey,
                    retryProcessFailedMessageInSecondsUnit,
                    cancellationToken,
                    messageBusProducer,
                    outboxBusMessageRepository,
                    logger,
                    serviceProvider);
        }
        else
        {
            await SaveAndTrySendNewOutboxMessageAsync(
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                unitOfWorkManager,
                outboxBusMessageRepository,
                serviceProvider);
        }
    }

    public static async Task SendExistingOutboxMessageAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        ILogger logger,
        IServiceProvider serviceProvider)
        where TMessage : class, new()
    {
        try
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);

            await UpdateExistingOutboxMessageProcessedAsync(
                existingOutboxMessage,
                cancellationToken,
                outboxBusMessageRepository);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "UpdateExistingOutboxMessageFailedInNewScopeAsync has been triggered");

            await serviceProvider.ExecuteInjectScopedAsync(
                UpdateExistingOutboxMessageFailedInNewUowAsync,
                existingOutboxMessage.Id,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
    }

    public static async Task SendExistingOutboxMessageInNewUowAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
        where TMessage : class, new()
    {
        using (var uow = unitOfWorkManager.Begin())
        {
            await serviceProvider.ExecuteInjectAsync(
                SendExistingOutboxMessageAsync<TMessage>,
                existingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
    }

    public static async Task UpdateExistingOutboxMessageProcessedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

        await outboxBusMessageRepository.UpdateAsync(existingOutboxMessage, dismissSendEvent: true, cancellationToken);
    }

    public static async Task UpdateExistingOutboxMessageFailedInNewUowAsync(
        string messageId,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
    {
        using (var uow = unitOfWorkManager.Begin())
        {
            await serviceProvider.ExecuteInjectAsync(
                UpdateExistingOutboxMessageFailedAsync,
                messageId,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
    }

    public static async Task UpdateExistingOutboxMessageFailedAsync(
        string messageId,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        ILogger logger)
    {
        var existingOutboxMessage = await outboxBusMessageRepository.GetByIdAsync(
            messageId,
            cancellationToken);

        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Failed;
        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.LastSendError = PlatformJsonSerializer.Serialize(
            new
            {
                exception.Message,
                exception.StackTrace
            });
        existingOutboxMessage.RetriedProcessCount = (existingOutboxMessage.RetriedProcessCount ?? 0) + 1;
        existingOutboxMessage.NextRetryProcessAfter = PlatformOutboxBusMessage.CalculateNextRetryProcessAfter(
            existingOutboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await outboxBusMessageRepository.UpdateAsync(existingOutboxMessage, dismissSendEvent: true, cancellationToken);

        LogSendOutboxMessageFailed(exception, existingOutboxMessage, logger);
    }

    protected static async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        IServiceProvider serviceProvider)
        where TMessage : class, new()
    {
        var newProcessingOutboxMessage = PlatformOutboxBusMessage.Create(
            message,
            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
            routingKey,
            PlatformOutboxBusMessage.SendStatuses.Processing);

        var existingProcessingOutboxMessage = await outboxBusMessageRepository.CreateAsync(
            newProcessingOutboxMessage,
            dismissSendEvent: true,
            cancellationToken);

        var currentUow = unitOfWorkManager.TryGetCurrentActiveUow();
        // WHY: Do not need to wait for uow completed if the uow for db do not handle actually transaction.
        // Can execute it immediately without waiting for uow to complete
        if (currentUow == null ||
            currentUow.IsNoTransactionUow() ||
            (currentUow is IPlatformAggregatedPersistenceUnitOfWork currentAggregatedPersistenceUow &&
             currentAggregatedPersistenceUow.IsNoTransactionUow(outboxBusMessageRepository.CurrentActiveUow())))
            await serviceProvider.ExecuteInjectAsync(
                SendExistingOutboxMessageAsync<TMessage>,
                existingProcessingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        else
            // Do not use async, just call.Wait()
            // WHY: Never use async lambda on event handler, because it's equivalent to async void, which fire async task and forget
            // this will lead to a lot of potential bug and issues.
            currentUow.OnCompleted += (sender, args) =>
            {
                // Try to process sending newProcessingOutboxMessage first time immediately after task completed
                // WHY: we can wait for the background process handle the message but try to do it
                // immediately if possible is better instead of waiting for the background process
                serviceProvider
                    .ExecuteInjectScopedAsync(
                        SendExistingOutboxMessageInNewUowAsync<TMessage>,
                        existingProcessingOutboxMessage,
                        message,
                        routingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        cancellationToken)
                    .WaitResult();
            };
    }

    protected static void LogSendOutboxMessageFailed(Exception exception, PlatformOutboxBusMessage existingOutboxMessage, ILogger logger)
    {
        logger.LogError(
            exception,
            $"Error Send message [RoutingKey:{{ExistingOutboxMessage_RoutingKey}}], [Type:{{ExistingOutboxMessage_MessageTypeFullName}}].{Environment.NewLine}" +
            $"Message Info: ${{ExistingOutboxMessage_JsonMessage}}.{Environment.NewLine}",
            existingOutboxMessage.RoutingKey,
            existingOutboxMessage.MessageTypeFullName,
            existingOutboxMessage.JsonMessage);
    }
}
