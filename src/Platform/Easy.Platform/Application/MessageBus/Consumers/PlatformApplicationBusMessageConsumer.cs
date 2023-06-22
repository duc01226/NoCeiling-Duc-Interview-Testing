using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers;

public interface IPlatformApplicationMessageBusConsumer : IPlatformMessageBusConsumer
{
    public PlatformInboxBusMessage HandleDirectlyExistingInboxMessage { get; set; }

    public bool AutoDeleteProcessedInboxEventMessage { get; set; }

    public bool IsInstanceExecutingFromInboxHelper { get; set; }

    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }
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
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerFactory)
    {
        UowManager = uowManager;
        InboxBusMessageRepo = serviceProvider.GetService<IPlatformInboxBusMessageRepository>();
        InboxConfig = serviceProvider.GetRequiredService<PlatformInboxConfig>();
        ServiceProvider = serviceProvider;

        IsInjectingUserContextAccessor = GetType().IsUsingGivenTypeViaConstructor<IPlatformApplicationUserContextAccessor>();
        if (IsInjectingUserContextAccessor)
            CreateLogger(loggerFactory)
                .LogError(
                    "{EventHandlerType} is injecting and using {IPlatformApplicationUserContextAccessor}, which will make the event handler could not run in background thread. " +
                    "The event sender must wait the handler to be finished. Should use the {RequestContext} info in the event instead.",
                    GetType().Name,
                    nameof(IPlatformApplicationUserContextAccessor),
                    nameof(PlatformCqrsEvent.RequestContext));
    }

    public virtual bool AutoBeginUow => true;
    public bool IsInjectingUserContextAccessor { get; set; }

    public PlatformInboxBusMessage HandleDirectlyExistingInboxMessage { get; set; }
    public bool AutoDeleteProcessedInboxEventMessage { get; set; }
    public bool IsInstanceExecutingFromInboxHelper { get; set; }
    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }

    protected override async Task ExecuteHandleLogicAsync(TMessage message, string routingKey)
    {
        if (InboxBusMessageRepo != null && !IsInstanceExecutingFromInboxHelper)
        {
            await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                ServiceProvider,
                consumer: this,
                inboxBusMessageRepository: InboxBusMessageRepo,
                inboxConfig: InboxConfig,
                message: message,
                routingKey: routingKey,
                loggerFactory: CreateGlobalLogger,
                retryProcessFailedMessageInSecondsUnit: InboxConfig.RetryProcessFailedMessageInSecondsUnit,
                allowProcessInBackgroundThread: AllowProcessInboxMessageInBackgroundThread,
                handleExistingInboxMessage: HandleDirectlyExistingInboxMessage,
                autoDeleteProcessedMessage: AutoDeleteProcessedInboxEventMessage,
                handleInUow: null);
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

    public static ILogger CreateGlobalLogger()
    {
        return CreateLogger(PlatformGlobal.LoggerFactory);
    }
}
