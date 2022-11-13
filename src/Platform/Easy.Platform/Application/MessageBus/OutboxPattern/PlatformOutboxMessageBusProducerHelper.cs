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
    private readonly ILogger<PlatformOutboxMessageBusProducerHelper> logger;
    private readonly IPlatformMessageBusProducer messageBusProducer;
    private readonly IPlatformOutboxBusMessageRepository outboxBusMessageRepository;
    private readonly PlatformOutboxConfig outboxConfig;
    private readonly IServiceProvider serviceProvider;
    private readonly IUnitOfWorkManager unitOfWorkManager;

    public PlatformOutboxMessageBusProducerHelper(
        PlatformOutboxConfig outboxConfig,
        ILogger<PlatformOutboxMessageBusProducerHelper> logger,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformMessageBusProducer messageBusProducer,
        IServiceProvider serviceProvider)
    {
        this.outboxConfig = outboxConfig;
        this.logger = logger;
        this.unitOfWorkManager = unitOfWorkManager;
        outboxBusMessageRepository = serviceProvider.GetService<IPlatformOutboxBusMessageRepository>();
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
        if (outboxBusMessageRepository != null)
        {
            var needToStartNewUow = !unitOfWorkManager.HasCurrentActiveUow() || outboxConfig.StandaloneUowForOutbox;

            var currentUow = needToStartNewUow
                ? unitOfWorkManager.Begin()
                : unitOfWorkManager.CurrentUow();

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
                        cancellationToken);
            }
            else
            {
                await SaveAndTrySendNewOutboxMessageAsync(
                    currentUow: currentUow,
                    message,
                    routingKey,
                    autoCompleteUow: needToStartNewUow,
                    retryProcessFailedMessageInSecondsUnit,
                    cancellationToken);
            }
        }
        else
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
        }
    }

    public async Task SendExistingOutboxMessageAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
        where TMessage : class, new()
    {
        try
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);

            await UpdateExistingOutboxMessageProcessedAsync(
                existingOutboxMessage.Id,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "UpdateExistingOutboxMessageFailedInNewScopeAsync has been triggered");

            await UpdateExistingOutboxMessageFailedInNewScopeAsync(
                existingOutboxMessage.Id,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
    }

    public async Task SendExistingOutboxMessageInNewScopeAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken) where TMessage : class, new()
    {
        using (var newScope = serviceProvider.CreateScope())
        {
            var outboxEventBusProducerHelper =
                newScope.ServiceProvider.GetService<PlatformOutboxMessageBusProducerHelper>();

            await outboxEventBusProducerHelper!.SendExistingOutboxMessageInNewUowAsync(
                existingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
    }

    public async Task SendExistingOutboxMessageInNewUowAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
        where TMessage : class, new()
    {
        using (var uow = unitOfWorkManager.Begin())
        {
            await SendExistingOutboxMessageAsync(
                existingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
    }

    public async Task UpdateExistingOutboxMessageProcessedAsync(
        string messageId,
        CancellationToken cancellationToken)
    {
        var existingOutboxMessage = await outboxBusMessageRepository.GetByIdAsync(
            messageId,
            cancellationToken);

        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

        await outboxBusMessageRepository.UpdateAsync(existingOutboxMessage, dismissSendEvent: true, cancellationToken: cancellationToken);
    }

    public async Task UpdateExistingOutboxMessageFailedInNewScopeAsync(
        string messageId,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
    {
        using (var newScope = serviceProvider.CreateScope())
        {
            var newScopeOutboxEventBusProducerHelper =
                newScope.ServiceProvider.GetService<PlatformOutboxMessageBusProducerHelper>();

            await newScopeOutboxEventBusProducerHelper!.UpdateExistingOutboxMessageFailedInNewUowAsync(
                messageId,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);
        }
    }

    public async Task UpdateExistingOutboxMessageFailedInNewUowAsync(
        string messageId,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
    {
        using (var uow = unitOfWorkManager.Begin())
        {
            await UpdateExistingOutboxMessageFailedAsync(
                messageId,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
    }

    public async Task UpdateExistingOutboxMessageFailedAsync(
        string messageId,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
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
            retriedProcessCount: existingOutboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await outboxBusMessageRepository.UpdateAsync(existingOutboxMessage, dismissSendEvent: true, cancellationToken: cancellationToken);

        LogSendOutboxMessageFailed(exception, existingOutboxMessage);
    }

    protected async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(
        IUnitOfWork currentUow,
        TMessage message,
        string routingKey,
        bool autoCompleteUow,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
        where TMessage : class, new()
    {
        var newProcessingOutboxMessage = PlatformOutboxBusMessage.Create(
            message,
            message.As<IPlatformTrackableBusMessage>()?.TrackingId,
            routingKey,
            PlatformOutboxBusMessage.SendStatuses.Processing);

        await outboxBusMessageRepository.CreateAsync(
            newProcessingOutboxMessage,
            dismissSendEvent: true,
            cancellationToken);

        // WHY: Do not need to wait for uow completed if the uow for db do not handle actually transaction.
        // Can execute it immediately without waiting for uow to complete
        if (currentUow.IsNoTransactionUow() ||
            (currentUow is IPlatformAggregatedPersistenceUnitOfWork currentAggregatedPersistenceUow &&
             currentAggregatedPersistenceUow.IsNoTransactionUow(outboxBusMessageRepository.CurrentActiveUow())))
            await SendExistingOutboxMessageAsync(
                newProcessingOutboxMessage,
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
                SendExistingOutboxMessageInNewScopeAsync(
                        newProcessingOutboxMessage,
                        message,
                        routingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        cancellationToken)
                    .WaitResult();
            };

        if (autoCompleteUow)
            await currentUow.CompleteAsync(cancellationToken);
    }

    protected void LogSendOutboxMessageFailed(Exception exception, PlatformOutboxBusMessage existingOutboxMessage)
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
