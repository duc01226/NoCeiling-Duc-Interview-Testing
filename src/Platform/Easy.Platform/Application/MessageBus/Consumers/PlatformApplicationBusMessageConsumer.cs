using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers;

public interface IPlatformApplicationMessageBusConsumer : IPlatformMessageBusConsumer
{
    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }

    public bool IsInstanceExecutingFromInboxHelper { get; set; }

    public static void LogError<TMessage>(
        ILogger logger,
        Type consumerType,
        TMessage message,
        string routingKey,
        Exception e)
        where TMessage : class, new()
    {
        logger.LogError(
            e,
            $"[{consumerType.FullName}] Error Consume message [RoutingKey:{{RoutingKey}}], [Type:{{BusMessage_Type}}].{Environment.NewLine}" +
            $"Message Info: {{BusMessage}}.{Environment.NewLine}",
            routingKey,
            message.GetType().GetNameOrGenericTypeName(),
            message.ToJson());
    }
}

public interface IPlatformApplicationMessageBusConsumer<in TMessage> : IPlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer
    where TMessage : class, new()
{
}

public abstract class PlatformApplicationMessageBusConsumer<TMessage> : PlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
    protected readonly IPlatformInboxBusMessageRepository InboxBusMessageRepo;
    protected readonly PlatformInboxConfig InboxConfig;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IUnitOfWorkManager UowManager;

    protected PlatformApplicationMessageBusConsumer(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerBuilder)
    {
        UowManager = uowManager;
        InboxBusMessageRepo = serviceProvider.GetService<IPlatformInboxBusMessageRepository>();
        InboxConfig = serviceProvider.GetRequiredService<PlatformInboxConfig>();
        ServiceProvider = serviceProvider;
    }

    public virtual bool AutoBeginUow => true;

    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }
    public bool IsInstanceExecutingFromInboxHelper { get; set; }

    public override async Task HandleAsync(TMessage message, string routingKey)
    {
        if (!HandleWhen(message, routingKey)) return;

        try
        {
            if (RetryOnFailedTimes > 0)
                // Retry RetryOnFailedTimes to help resilient consumer. Sometime parallel, create/update concurrency could lead to error
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(() => DoHandleAsync(message, routingKey), retryCount: RetryOnFailedTimes);
            else
                await DoHandleAsync(message, routingKey);
        }
        catch (Exception e)
        {
            IPlatformApplicationMessageBusConsumer.LogError(Logger, GetType(), message, routingKey, e);
            throw;
        }

        async Task DoHandleAsync(TMessage message, string routingKey)
        {
            if (InboxBusMessageRepo != null && !IsInstanceExecutingFromInboxHelper)
            {
                await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                    ServiceProvider,
                    consumer: this,
                    InboxBusMessageRepo,
                    message,
                    routingKey,
                    CreateGlobalLogger,
                    InboxConfig.RetryProcessFailedMessageInSecondsUnit,
                    HandleExistingInboxMessage);
            }
            else
            {
                if (AutoBeginUow)
                    using (var uow = UowManager.Begin())
                    {
                        await HandleLogicAsync(message, routingKey);
                        await uow.CompleteAsync();
                    }
                else
                    await HandleLogicAsync(message, routingKey);
            }
        }
    }

    public static ILogger CreateGlobalLogger()
    {
        return CreateLogger(PlatformGlobal.LoggerFactory);
    }
}
