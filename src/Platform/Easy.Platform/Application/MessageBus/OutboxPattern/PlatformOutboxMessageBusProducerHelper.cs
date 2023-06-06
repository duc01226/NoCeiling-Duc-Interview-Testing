using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxMessageBusProducerHelper : IPlatformHelper
{
    public const int DefaultResilientRetiredCount = 5;
    public const int DefaultResilientRetiredDelayMilliseconds = 200;

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
                        IUnitOfWorkManager unitOfWorkManager) => SendOutboxMessageAsync(
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
            else
                await serviceProvider.ExecuteInjectAsync(
                    (
                        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
                        IPlatformMessageBusProducer messageBusProducer,
                        IUnitOfWorkManager unitOfWorkManager) => SendOutboxMessageAsync(
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
        IUnitOfWorkManager unitOfWorkManager) where TMessage : class, new()
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

    public static async Task SendExistingOutboxMessageAsync<TMessage>(
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
                        existingOutboxMessage,
                        cancellationToken,
                        outboxBusMessageRepository);
                },
                sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                retryCount: DefaultResilientRetiredCount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SendExistingOutboxMessageAsync failed. [[Error:{Error}]]", exception.Message);

            await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                UpdateExistingOutboxMessageFailedInNewUowAsync,
                existingOutboxMessage,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger);
        }
    }

    public static async Task SendExistingOutboxMessageInNewUowAsync<TMessage>(
        PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
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
                cancellationToken,
                logger);

            await uow.CompleteAsync(cancellationToken);
        }
    }

    public static async Task UpdateExistingOutboxMessageProcessedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                existingOutboxMessage.LastSendDate = DateTime.UtcNow;
                existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

                await outboxBusMessageRepository.UpdateAsync(existingOutboxMessage, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);
            },
            sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            retryCount: DefaultResilientRetiredCount);
    }

    public static async Task UpdateExistingOutboxMessageFailedInNewUowAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
    {
        using (var uow = unitOfWorkManager.Begin())
        {
            await serviceProvider.ExecuteInjectAsync(
                UpdateExistingOutboxMessageFailedAsync,
                existingOutboxMessage,
                exception,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger);

            await uow.CompleteAsync(cancellationToken);
        }
    }

    public static async Task UpdateExistingOutboxMessageFailedAsync(
        PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
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

                await outboxBusMessageRepository.CreateOrUpdateAsync(existingOutboxMessage, dismissSendEvent: true, sendEntityEventConfigure: null, cancellationToken);

                LogSendOutboxMessageFailed(exception, existingOutboxMessage, logger);
            },
            sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            retryCount: DefaultResilientRetiredCount);
    }

    protected async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string sourceOutboxUowId,
        CancellationToken cancellationToken,
        ILogger logger,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        var messageTrackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

        var existedOutboxMessage = messageTrackId != null
            ? await outboxBusMessageRepository.FirstOrDefaultAsync(
                p => p.Id == PlatformOutboxBusMessage.BuildId(messageTrackId),
                cancellationToken)
            : null;

        var newOutboxMessage = existedOutboxMessage == null
            ? await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => outboxBusMessageRepository.CreateAsync(
                    PlatformOutboxBusMessage.Create(
                        message,
                        messageTrackId,
                        routingKey,
                        PlatformOutboxBusMessage.SendStatuses.Processing),
                    dismissSendEvent: true,
                    sendEntityEventConfigure: null,
                    cancellationToken),
                sleepDurationProvider: retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                retryCount: DefaultResilientRetiredCount)
            : null;

        var toProcessInboxMessage = existedOutboxMessage ?? newOutboxMessage;

        if (existedOutboxMessage == null ||
            PlatformOutboxBusMessage.CanHandleMessagesExpr(outboxConfig.MessageProcessingMaxSeconds).Compile()(existedOutboxMessage))
        {
            var currentActiveUow = unitOfWorkManager.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId);
            // WHY: Do not need to wait for uow completed if the uow for db do not handle actually transaction.
            // Can execute it immediately without waiting for uow to complete
            if (currentActiveUow == null)
                Util.TaskRunner.QueueActionInBackground(
                    async () => await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                        SendExistingOutboxMessageAsync<TMessage>,
                        toProcessInboxMessage,
                        message,
                        routingKey,
                        retryProcessFailedMessageInSecondsUnit,
                        cancellationToken,
                        logger),
                    loggerFactory: CreateLogger,
                    cancellationToken: cancellationToken);
            else
                currentActiveUow.OnCompletedActions.Add(
                    async () =>
                    {
                        // Try to process sending newProcessingOutboxMessage first time immediately after task completed
                        // WHY: we can wait for the background process handle the message but try to do it
                        // immediately if possible is better instead of waiting for the background process
                        await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
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

    private static ILogger CreateLogger()
    {
        return PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformOutboxMessageBusProducerHelper));
    }

    protected static void LogSendOutboxMessageFailed(Exception exception, PlatformOutboxBusMessage existingOutboxMessage, ILogger logger)
    {
        logger.LogError(
            exception,
            $"Error Send message [Type:{{ExistingOutboxMessage_MessageTypeFullName}}]; [[Error:{{Error}}]].{Environment.NewLine}" +
            $"Message Info: ${{ExistingOutboxMessage_JsonMessage}}.{Environment.NewLine}",
            exception.Message,
            existingOutboxMessage.RoutingKey,
            existingOutboxMessage.MessageTypeFullName,
            existingOutboxMessage.JsonMessage);
    }
}
