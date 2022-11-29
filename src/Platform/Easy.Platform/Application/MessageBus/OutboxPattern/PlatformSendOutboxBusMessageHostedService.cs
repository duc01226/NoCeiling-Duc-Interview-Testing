using Easy.Platform.Application.Context;
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

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

/// <summary>
/// Run in an interval to scan messages in OutboxCollection in database, check new message to send it
/// </summary>
public class PlatformSendOutboxBusMessageHostedService : PlatformIntervalProcessHostedService
{
    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private bool isProcessing;

    public PlatformSendOutboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        OutboxConfig = outboxConfig;
    }

    protected PlatformOutboxConfig OutboxConfig { get; }

    public static bool MatchImplementation(ServiceDescriptor serviceDescriptor)
    {
        return MatchImplementation(serviceDescriptor.ImplementationType) ||
               MatchImplementation(serviceDescriptor.ImplementationInstance?.GetType());
    }

    public static bool MatchImplementation(Type implementationType)
    {
        return implementationType?.IsAssignableTo(typeof(PlatformSendOutboxBusMessageHostedService)) == true;
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing)
            return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => SendOutboxEventBusMessages(cancellationToken),
                retryAttempt => 10.Seconds(),
                retryCount: ProcessSendMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry, ctx) =>
                {
                    Logger.LogWarning(
                        ex,
                        $"Retry SendOutboxEventBusMessages {currentRetry} time(s) failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                $"SendOutboxEventBusMessages failed with error: {ex.Message}. [ApplicationName:{applicationSettingContext.ApplicationName}]. [ApplicationAssembly:{applicationSettingContext.ApplicationAssembly.FullName}]");
        }

        isProcessing = false;
    }

    protected virtual async Task SendOutboxEventBusMessages(CancellationToken cancellationToken)
    {
        do
        {
            var toHandleMessages = await PopToHandleOutboxEventBusMessages(cancellationToken);

            await toHandleMessages
                .Select(
                    async toHandleOutboxMessage =>
                    {
                        using (var scope = ServiceProvider.CreateScope())
                        {
                            try
                            {
                                await SendMessageToBusAsync(
                                    scope,
                                    toHandleOutboxMessage,
                                    OutboxConfig.RetryProcessFailedMessageInSecondsUnit,
                                    cancellationToken);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError(
                                    e,
                                    "[PlatformSendOutboxEventBusMessageHostedService] Failed to produce outbox message. " +
                                    $"Id:{toHandleOutboxMessage.Id} failed. " +
                                    "Message Content:{OutboxMessage}",
                                    toHandleOutboxMessage.AsFormattedJson());
                            }
                        }
                    })
                .WhenAll();
        } while (await IsAnyMessagesToHandleAsync());
    }

    protected async Task<bool> IsAnyMessagesToHandleAsync()
    {
        return await ServiceProvider.ExecuteInjectScopedAsync<bool>(
            async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
            {
                var result = await outboxEventBusMessageRepo!.AnyAsync(
                    PlatformOutboxBusMessage.ToHandleOutboxEventBusMessagesExpr(MessageProcessingMaximumTimeInSeconds()));

                return result;
            });
    }

    protected virtual async Task SendMessageToBusAsync(
        IServiceScope scope,
        PlatformOutboxBusMessage toHandleOutboxMessage,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
    {
        await scope.ExecuteInjectAsync(
            async (PlatformOutboxMessageBusProducerHelper outboxEventBusProducerHelper, IUnitOfWorkManager uowManager) =>
            {
                var messageType = ResolveMessageType(toHandleOutboxMessage);

                using (var uow = uowManager.Begin())
                {
                    if (messageType != null)
                    {
                        var message = PlatformJsonSerializer.Deserialize(
                            toHandleOutboxMessage.JsonMessage,
                            messageType);

                        await outboxEventBusProducerHelper!.HandleSendingOutboxMessageAsync(
                            message,
                            toHandleOutboxMessage.RoutingKey,
                            retryProcessFailedMessageInSecondsUnit,
                            handleExistingOutboxMessageId: toHandleOutboxMessage.Id,
                            cancellationToken);
                    }
                    else
                    {
                        await outboxEventBusProducerHelper!.UpdateExistingOutboxMessageFailedAsync(
                            toHandleOutboxMessage.Id,
                            new Exception(
                                $"[{GetType().Name}] Error resolve outbox message type " +
                                $"[TypeName:{toHandleOutboxMessage.MessageTypeFullName}]. OutboxId:{toHandleOutboxMessage.Id}"),
                            retryProcessFailedMessageInSecondsUnit,
                            cancellationToken);
                    }

                    await uow.CompleteAsync(cancellationToken);
                }
            });
    }

    protected async Task<List<PlatformOutboxBusMessage>> PopToHandleOutboxEventBusMessages(
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformOutboxBusMessage>>(
                async (IUnitOfWorkManager uowManager, IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                {
                    using (var uow = uowManager.Begin())
                    {
                        var toHandleMessages = await outboxEventBusMessageRepo.GetAllAsync(
                            queryBuilder: query => query
                                .Where(PlatformOutboxBusMessage.ToHandleOutboxEventBusMessagesExpr(MessageProcessingMaximumTimeInSeconds()))
                                .OrderBy(p => p.LastSendDate)
                                .Take(NumberOfProcessMessagesBatch()),
                            cancellationToken);

                        toHandleMessages.ForEach(
                            p =>
                            {
                                p.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processing;
                                p.LastSendDate = DateTime.UtcNow;
                            });

                        await outboxEventBusMessageRepo.UpdateManyAsync(
                            toHandleMessages,
                            cancellationToken: cancellationToken);

                        await uow.CompleteAsync(cancellationToken);

                        return toHandleMessages;
                    }
                });
        }
        catch (PlatformDomainRowVersionConflictException conflictDomainException)
        {
            Logger.LogWarning(
                conflictDomainException,
                "Some other producer instance has been handling some outbox messages, which lead to row version conflict (support multi service instance running concurrently). This is as expected so just warning.");

            // WHY: Because support multi service instance running concurrently,
            // get row version conflict is expected, so just retry again to get unprocessed outbox messages
            return await PopToHandleOutboxEventBusMessages(cancellationToken);
        }
    }

    protected virtual int NumberOfProcessMessagesBatch()
    {
        return Environment.ProcessorCount;
    }

    protected virtual int ProcessSendMessageRetryCount()
    {
        return 10;
    }

    /// <summary>
    /// To config how long a message can live in the database as Processing status in seconds. Default is 3600 seconds;
    /// This to handle that if message for some reason has been set as Processing but failed to process and has not been set
    /// back to failed.
    /// </summary>
    protected virtual double MessageProcessingMaximumTimeInSeconds()
    {
        return 3600;
    }

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope => scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    private Type ResolveMessageType(PlatformOutboxBusMessage toHandleOutboxMessage)
    {
        var messageType =
            Type.GetType(toHandleOutboxMessage.MessageTypeFullName, throwOnError: false) ??
            ServiceProvider
                .GetService<IPlatformMessageBusScanner>()!
                .ScanAssemblies()
                .ConcatSingle(typeof(PlatformModule).Assembly)
                .Select(assembly => assembly.GetType(toHandleOutboxMessage.MessageTypeFullName))
                .FirstOrDefault(p => p != null);

        return messageType;
    }
}
