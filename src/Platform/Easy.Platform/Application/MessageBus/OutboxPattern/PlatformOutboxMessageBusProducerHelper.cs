using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxMessageBusProducerHelper : IPlatformHelper
{
    public const int DefaultResilientRetiredCount = 2;
    public const int DefaultResilientRetiredDelayMilliseconds = 200;

    private readonly IPlatformMessageBusProducer messageBusProducer;
    private readonly PlatformOutboxConfig outboxConfig;
    private readonly IPlatformRootServiceProvider rootServiceProvider;
    private readonly IServiceProvider serviceProvider;

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

    public async Task HandleSendingOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformOutboxBusMessage handleExistingOutboxMessage = null,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (serviceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null)
        {
            if (outboxConfig.StandaloneScopeForOutbox)
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
                        cancellationToken,
                        CreateLogger(),
                        outboxBusMessageRepository,
                        messageBusProducer,
                        unitOfWorkManager));
            else
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
                        cancellationToken,
                        CreateLogger(),
                        outboxBusMessageRepository,
                        messageBusProducer,
                        unitOfWorkManager));
        }
        else
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
        }
    }

    public async Task SendOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformOutboxBusMessage handleExistingOutboxMessage,
        string sourceOutboxUowId,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformUnitOfWorkManager unitOfWorkManager) where TMessage : class, new()
    {
        if (handleExistingOutboxMessage != null &&
            PlatformOutboxBusMessage.CanHandleMessagesExpr(outboxConfig.MessageProcessingMaxSeconds).Compile()(handleExistingOutboxMessage))
            await SendExistingOutboxMessageAsync(
                handleExistingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger,
                messageBusProducer,
                outboxBusMessageRepository);
        else if (handleExistingOutboxMessage == null)
            await SaveAndTrySendNewOutboxMessageAsync(
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                sourceOutboxUowId,
                cancellationToken,
                logger,
                unitOfWorkManager,
                outboxBusMessageRepository);
    }

    public async Task SendExistingOutboxMessageAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await messageBusProducer.SendAsync(message, routingKey, cancellationToken);

                    await UpdateExistingOutboxMessageProcessedAsync(
                        rootServiceProvider,
                        existingOutboxMessage,
                        cancellationToken);
                },
                sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                retryCount: DefaultResilientRetiredCount,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SendExistingOutboxMessageAsync failed. [[Error:{Error}]]", exception.Message);

            await UpdateExistingOutboxMessageFailedInNewScopeAsync(existingOutboxMessage, exception, retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

    public async Task SendExistingOutboxMessageInNewUowAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
        where TMessage : class, new()
    {
        try
        {
            await serviceProvider.ExecuteInjectAsync(
                SendExistingOutboxMessageAsync<TMessage>,
                existingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SendExistingOutboxMessageInNewUowAsync failed. [[Error:{Error}]]", exception.Message);

            await UpdateExistingOutboxMessageFailedInNewScopeAsync(existingOutboxMessage, exception, retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

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
                        try
                        {
                            toUpdateOutboxMessage.LastSendDate = DateTime.UtcNow;
                            toUpdateOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

                            await outboxBusMessageRepository.UpdateAsync(toUpdateOutboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);
                        }
                        catch (PlatformDomainRowVersionConflictException)
                        {
                            toUpdateOutboxMessage = await serviceProvider.ExecuteInjectScopedAsync<PlatformOutboxBusMessage>(
                                (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                                    outboxBusMessageRepository.GetByIdAsync(toUpdateOutboxMessage.Id, cancellationToken));
                            throw;
                        }
                    });
            },
            sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    public async Task UpdateExistingOutboxMessageFailedInNewScopeAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await rootServiceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                        {
                            var latestCurrentExistingOutboxMessage = await outboxBusMessageRepository.FirstOrDefaultAsync(
                                p => p.Id == existingOutboxMessage.Id,
                                cancellationToken);

                            if (latestCurrentExistingOutboxMessage != null)
                                await UpdateExistingOutboxMessageFailedAsync(
                                    latestCurrentExistingOutboxMessage,
                                    exception,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    logger,
                                    outboxBusMessageRepository);
                        });
                },
                retryCount: DefaultResilientRetiredCount,
                sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelayMilliseconds.Milliseconds(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "UpdateExistingOutboxMessageFailedInNewUowAsync failed. [[Error:{Error}]]].",
                ex.Message);
        }
    }

    private static async Task UpdateExistingOutboxMessageFailedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Failed;
        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.LastSendError = exception.Serialize();
        existingOutboxMessage.RetriedProcessCount = (existingOutboxMessage.RetriedProcessCount ?? 0) + 1;
        existingOutboxMessage.NextRetryProcessAfter = PlatformOutboxBusMessage.CalculateNextRetryProcessAfter(
            existingOutboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await outboxBusMessageRepository.CreateOrUpdateAsync(existingOutboxMessage, dismissSendEvent: true, eventCustomConfig: null, cancellationToken);

        LogSendOutboxMessageFailed(exception, existingOutboxMessage, logger);
    }

    protected async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string sourceOutboxUowId,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var messageTrackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

                var existedOutboxMessage = messageTrackId != null
                    ? await outboxBusMessageRepository.FirstOrDefaultAsync(
                        p => p.Id == PlatformOutboxBusMessage.BuildId(messageTrackId),
                        cancellationToken)
                    : null;

                var newOutboxMessage = existedOutboxMessage == null
                    ? await outboxBusMessageRepository.CreateAsync(
                        PlatformOutboxBusMessage.Create(
                            message,
                            messageTrackId,
                            routingKey,
                            PlatformOutboxBusMessage.SendStatuses.Processing),
                        dismissSendEvent: true,
                        eventCustomConfig: null,
                        cancellationToken)
                    : null;

                var toProcessInboxMessage = existedOutboxMessage ?? newOutboxMessage;

                if (existedOutboxMessage == null ||
                    PlatformOutboxBusMessage.CanHandleMessagesExpr(outboxConfig.MessageProcessingMaxSeconds).Compile()(existedOutboxMessage))
                {
                    var currentActiveUow = sourceOutboxUowId != null
                        ? unitOfWorkManager.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId)
                        : null;
                    // WHY: Do not need to wait for uow completed if the uow for db do not handle actually transaction.
                    // Can execute it immediately without waiting for uow to complete
                    if (currentActiveUow == null || currentActiveUow.IsPseudoTransactionUow())
                    {
                        // Check try CompleteAsync current active uow if any to ensure that newOutboxMessage will be saved
                        // Do this to fix if someone open uow without complete/save it for some legacy project
                        if (outboxBusMessageRepository.UowManager().TryGetCurrentActiveUow() != null)
                            await outboxBusMessageRepository.UowManager().CurrentActiveUow().SaveChangesAsync(cancellationToken);

                        Util.TaskRunner.QueueActionInBackground(
                            async () => await rootServiceProvider.ExecuteInjectScopedAsync(
                                SendExistingOutboxMessageAsync<TMessage>,
                                toProcessInboxMessage,
                                message,
                                routingKey,
                                retryProcessFailedMessageInSecondsUnit,
                                cancellationToken,
                                logger),
                            loggerFactory: CreateLogger,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        currentActiveUow.OnSaveChangesCompletedActions.Add(
                            async () =>
                            {
                                // Try to process sending newProcessingOutboxMessage first time immediately after task completed
                                // WHY: we can wait for the background process handle the message but try to do it
                                // immediately if possible is better instead of waiting for the background process
                                await rootServiceProvider.ExecuteInjectScopedAsync(
                                    SendExistingOutboxMessageInNewUowAsync<TMessage>,
                                    toProcessInboxMessage,
                                    message,
                                    routingKey,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    logger);
                            });
                    }
                }
            },
            sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            retryCount: DefaultResilientRetiredCount,
            cancellationToken: cancellationToken);
    }

    protected ILogger CreateLogger()
    {
        return rootServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PlatformOutboxMessageBusProducerHelper));
    }

    protected static void LogSendOutboxMessageFailed(Exception exception, PlatformOutboxBusMessage existingOutboxMessage, ILogger logger)
    {
        logger.LogError(
            exception,
            "Error Send message [Type:{ExistingOutboxMessage_MessageTypeFullName}]; [[Error:{Error}]]. " +
            "Message Info: ${ExistingOutboxMessage_JsonMessage}.",
            existingOutboxMessage.MessageTypeFullName,
            exception.Message,
            existingOutboxMessage.JsonMessage);
    }
}
