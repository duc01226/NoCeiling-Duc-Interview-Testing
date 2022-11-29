using Easy.Platform.Application.Context;
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
            .ToDictionary(PlatformInboxMessageBusConsumerHelper.GetConsumerByValue);
    }

    protected Dictionary<string, Type> ConsumerByNameToTypeDic { get; }

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

            // Handle parallel consumers for sameTrackIdMessages (1 track message could be handled by many consumers)
            // ForEachAsync to handle one by one consumers group (Handle each message at a time)
            await toHandleMessages
                .GroupBy(p => p.GetTrackId())
                .ForEachAsync(
                    p =>
                    {
                        var sameTrackIdMessages = p.ToList();

                        return sameTrackIdMessages
                            .Select(HandleInboxMessageAsync)
                            .WhenAll();
                    });
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
                        toHandleInboxMessage.AsJson());
                }
            }
        }
    }

    protected async Task<bool> IsAnyMessagesToHandleAsync()
    {
        return await ServiceProvider.ExecuteInjectScopedAsync<bool>(
            async (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
            {
                var result = await inboxEventBusMessageRepo!.AnyAsync(
                    PlatformInboxBusMessage.ToHandleInboxEventBusMessagesExpr(MessageProcessingMaximumTimeInSeconds()));

                return result;
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
                .As<IPlatformMessageBusConsumer>()
                .With(_ => _.HandleExistingInboxMessageTrackId = toHandleInboxMessage.GetTrackId());

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
                    Logger,
                    cancellationToken);
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

    // Default should be 1 per time like multi thread/instance handle once at a time. Also we have not found out way to process parallel messages
    // because need to detect message order dependency for a same person or entity target
    // Support deploy multiple instance horizontal scale
    protected virtual int NumberOfProcessMessagesBatch()
    {
        return Environment.ProcessorCount;
    }

    protected virtual int ProcessConsumeMessageRetryCount()
    {
        return 10;
    }

    /// <summary>
    /// To config how long a message can live in the database as Processing status in seconds. Default is 3600 * 24 seconds;
    /// This to handle that if message for some reason has been set as Processing but failed to process and has not been set
    /// back to failed.
    /// </summary>
    protected virtual double MessageProcessingMaximumTimeInSeconds()
    {
        return 3600 * 24;
    }

    /// <summary>
    /// Config the time to true to log consumer process time. Default is true
    /// </summary>
    protected virtual bool IsLogConsumerProcessTime()
    {
        return true;
    }

    /// <summary>
    /// Config the time in milliseconds to log warning if the process consumer time is over
    /// LogConsumerProcessWarningTimeMilliseconds. Default is 5000
    /// </summary>
    protected virtual double LogErrorSlowProcessWarningTimeMilliseconds()
    {
        return 5000;
    }

    /// <summary>
    /// This is used to calculate the next retry process message time.
    /// Ex: NextRetryProcessAfter = DateTime.UtcNow.AddSeconds(retryProcessFailedMessageInSecondsUnit * Math.Pow(2,
    /// retriedProcessCount ?? 0));
    /// </summary>
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
