using Easy.Platform.Application.Context;
using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Hosting;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

/// <summary>
/// Run in an interval to scan messages in InboxCollection in database, check new message to consume it
/// </summary>
public class PlatformConsumeInboxBusMessageHostedService : PlatformIntervalProcessHostedService
{
    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly PlatformInboxConfig inboxConfig;

    private bool isProcessing;

    public PlatformConsumeInboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformInboxConfig inboxConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        this.inboxConfig = inboxConfig;
        ConsumerByNameToTypeDic = messageBusScanner
            .ScanAllDefinedConsumerTypes()
            .ToDictionary(PlatformInboxBusMessage.GetConsumerByValue);
        InvokeConsumerLogger = PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformMessageBusConsumer));
    }

    protected Dictionary<string, Type> ConsumerByNameToTypeDic { get; }

    protected ILogger InvokeConsumerLogger { get; }

    public static bool MatchImplementation(ServiceDescriptor serviceDescriptor)
    {
        return MatchImplementation(serviceDescriptor.ImplementationType) ||
               MatchImplementation(serviceDescriptor.ImplementationInstance?.GetType());
    }

    public static bool MatchImplementation(Type implementationType)
    {
        return implementationType?.IsAssignableTo(typeof(PlatformConsumeInboxBusMessageHostedService)) == true;
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing)
            return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the database is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => ConsumeInboxEventBusMessages(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: ProcessConsumeMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    Logger.LogWarning(
                        ex,
                        $"Retry ConsumeInboxEventBusMessages {currentRetry} time(s) failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                $"Retry ConsumeInboxEventBusMessages failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
        }

        isProcessing = false;
    }

    protected virtual async Task ConsumeInboxEventBusMessages(CancellationToken cancellationToken)
    {
        do
        {
            var toHandleMessages = await PopToHandleInboxEventBusMessages(cancellationToken);

            // Group by TrackId to handling multiple consumer with same message parallel
            await toHandleMessages
                .GroupBy(p => p.GetTrackId())
                .ForEachAsync(consumerMessages => consumerMessages.Select(HandleInboxMessageAsync).WhenAll());
        } while (await IsAnyMessagesToHandleAsync());

        async Task HandleInboxMessageAsync(PlatformInboxBusMessage toHandleInboxMessage)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                try
                {
                    await InvokeConsumerAsync(
                        scope,
                        toHandleInboxMessage,
                        cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        e,
                        "[PlatformConsumeInboxEventBusMessageHostedService] Try to consume inbox message with Id:{MessageId} failed. Message Content:{InboxMessage}",
                        toHandleInboxMessage.Id,
                        toHandleInboxMessage.ToJson());
                }
            }
        }
    }

    protected Task<bool> IsAnyMessagesToHandleAsync()
    {
        return ServiceProvider.ExecuteInjectScopedAsync<bool>(
            (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
            {
                return inboxEventBusMessageRepo!.AnyAsync(
                    PlatformInboxBusMessage.ToHandleInboxEventBusMessagesExpr(MessageProcessingMaximumTimeInSeconds()));
            });
    }

    protected virtual async Task InvokeConsumerAsync(
        IServiceScope scope,
        PlatformInboxBusMessage toHandleInboxMessage,
        CancellationToken cancellationToken)
    {
        var consumerType = ResolveConsumerType(toHandleInboxMessage);

        if (consumerType != null)
        {
            var consumer = scope.ServiceProvider.GetService(consumerType)
                .As<IPlatformApplicationMessageBusConsumer>()
                .With(_ => _.HandleDirectlyExistingInboxMessage = toHandleInboxMessage);

            var consumerMessageType = PlatformMessageBusConsumer.GetConsumerMessageType(consumer);

            var busMessage = Util.TaskRunner.CatchExceptionContinueThrow(
                () => PlatformJsonSerializer.Deserialize(
                    toHandleInboxMessage.JsonMessage,
                    consumerMessageType,
                    consumer.CustomJsonSerializerOptions()),
                ex => Logger.LogError(
                    ex,
                    $"RabbitMQ parsing message to {consumerMessageType.Name} error for the routing key {toHandleInboxMessage.RoutingKey}.{Environment.NewLine} Body: {{InboxMessage}}",
                    toHandleInboxMessage.JsonMessage));

            if (busMessage != null)
                await PlatformMessageBusConsumer.InvokeConsumerAsync(
                    consumer,
                    busMessage,
                    toHandleInboxMessage.RoutingKey,
                    IsLogConsumerProcessTime(),
                    LogErrorSlowProcessWarningTimeMilliseconds(),
                    InvokeConsumerLogger);
        }
        else
        {
            await PlatformInboxMessageBusConsumerHelper.UpdateFailedInboxMessageAsync(
                scope.ServiceProvider,
                toHandleInboxMessage.Id,
                new Exception(
                    $"[{GetType().Name}] Error resolve consumer type {toHandleInboxMessage.ConsumerBy}. InboxId:{toHandleInboxMessage.Id} "),
                RetryProcessFailedMessageDelayTimeInSecondsUnit(),
                cancellationToken);
        }
    }

    protected async Task<List<PlatformInboxBusMessage>> PopToHandleInboxEventBusMessages(
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformInboxBusMessage>>(
                async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    using (var uow = uowManager!.Begin())
                    {
                        var toHandleMessages = await inboxEventBusMessageRepo.GetAllAsync(
                            queryBuilder: query => query
                                .Where(PlatformInboxBusMessage.ToHandleInboxEventBusMessagesExpr(MessageProcessingMaximumTimeInSeconds()))
                                .OrderBy(p => p.LastConsumeDate)
                                .Take(NumberOfProcessMessagesBatch()),
                            cancellationToken);

                        toHandleMessages.ForEach(
                            p =>
                            {
                                p.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processing;
                                p.LastConsumeDate = DateTime.UtcNow;
                            });

                        await inboxEventBusMessageRepo.UpdateManyAsync(
                            toHandleMessages,
                            dismissSendEvent: true,
                            cancellationToken);

                        await uow.CompleteAsync(cancellationToken);

                        return toHandleMessages;
                    }
                });
        }
        catch (PlatformDomainRowVersionConflictException conflictDomainException)
        {
            Logger.LogWarning(
                conflictDomainException,
                "Some other consumer instance has been handling some inbox messages (support multi service instance running concurrently), which lead to row version conflict. This is as expected so just warning.");

            // WHY: Because support multi service instance running concurrently,
            // get row version conflict is expected, so just retry again to get unprocessed inbox messages
            return await PopToHandleInboxEventBusMessages(cancellationToken);
        }
    }

    /// <inheritdoc cref="PlatformInboxConfig.NumberOfProcessConsumeInboxMessagesBatch" />
    protected virtual int NumberOfProcessMessagesBatch()
    {
        return inboxConfig.NumberOfProcessConsumeInboxMessagesBatch;
    }

    protected virtual int ProcessConsumeMessageRetryCount()
    {
        return inboxConfig.ProcessConsumeMessageRetryCount;
    }

    /// <inheritdoc cref="PlatformInboxConfig.MessageProcessingMaximumTimeInSeconds" />
    protected virtual double MessageProcessingMaximumTimeInSeconds()
    {
        return inboxConfig.MessageProcessingMaximumTimeInSeconds;
    }

    /// <inheritdoc cref="PlatformInboxConfig.IsLogConsumerProcessTime" />
    protected virtual bool IsLogConsumerProcessTime()
    {
        return inboxConfig.IsLogConsumerProcessTime;
    }

    /// <inheritdoc cref="PlatformInboxConfig.LogErrorSlowProcessWarningTimeMilliseconds" />
    protected virtual double LogErrorSlowProcessWarningTimeMilliseconds()
    {
        return inboxConfig.LogErrorSlowProcessWarningTimeMilliseconds;
    }

    /// <inheritdoc cref="PlatformInboxConfig.RetryProcessFailedMessageInSecondsUnit" />
    protected virtual double RetryProcessFailedMessageDelayTimeInSecondsUnit()
    {
        return inboxConfig.RetryProcessFailedMessageInSecondsUnit;
    }

    protected bool HasInboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);
    }

    private Type ResolveConsumerType(PlatformInboxBusMessage toHandleInboxMessage)
    {
        return ConsumerByNameToTypeDic.GetValueOrDefault(toHandleInboxMessage.ConsumerBy, null);
    }
}
