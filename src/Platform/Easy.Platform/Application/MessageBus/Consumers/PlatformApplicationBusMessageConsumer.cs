using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers;

// <summary>
/// Represents a message bus consumer for platform applications.
/// </summary>
public interface IPlatformApplicationMessageBusConsumer : IPlatformMessageBusConsumer
{
    /// <summary>
    /// Gets or sets the message to be handled directly if it exists in the inbox.
    /// </summary>
    public PlatformInboxBusMessage HandleDirectlyExistingInboxMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the processed inbox event message should be automatically deleted.
    /// </summary>
    public bool AutoDeleteProcessedInboxEventMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is executing from the inbox helper.
    /// </summary>
    public bool IsInstanceExecutingFromInboxHelper { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow processing the inbox message in a background thread.
    /// </summary>
    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }

    /// <summary>
    /// Gets or sets the maximum timeout for executing inbox consumer.
    /// </summary>
    public TimeSpan? InboxProcessingMaxTimeout { get; set; }
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
    protected readonly IPlatformRootServiceProvider RootServiceProvider;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IUnitOfWorkManager UowManager;

    protected PlatformApplicationMessageBusConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory)
    {
        UowManager = uowManager;
        InboxBusMessageRepo = serviceProvider.GetService<IPlatformInboxBusMessageRepository>();
        InboxConfig = serviceProvider.GetRequiredService<PlatformInboxConfig>();
        ServiceProvider = serviceProvider;
        RootServiceProvider = rootServiceProvider;

        IsInjectingUserContextAccessor = GetType().IsUsingGivenTypeViaConstructor<IPlatformApplicationRequestContextAccessor>();
        if (IsInjectingUserContextAccessor)
            CreateLogger(loggerFactory)
                .LogError(
                    "{EventHandlerType} is injecting and using {IPlatformApplicationRequestContextAccessor}, which will make the event handler could not run in background thread. " +
                    "The event sender must wait the handler to be finished. Should use the {RequestContext} info in the event instead.",
                    GetType().Name,
                    nameof(IPlatformApplicationRequestContextAccessor),
                    nameof(PlatformCqrsEvent.RequestContext));
    }

    public virtual bool AutoBeginUow => true;
    public bool IsInjectingUserContextAccessor { get; set; }

    protected IPlatformApplicationRequestContextAccessor UserContextAccessor =>
        ServiceProvider.GetRequiredService<IPlatformApplicationRequestContextAccessor>();

    public PlatformInboxBusMessage HandleDirectlyExistingInboxMessage { get; set; }
    public bool AutoDeleteProcessedInboxEventMessage { get; set; }
    public bool IsInstanceExecutingFromInboxHelper { get; set; }
    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }
    public TimeSpan? InboxProcessingMaxTimeout { get; set; }

    protected override async Task ExecuteHandleLogicAsync(TMessage message, string routingKey)
    {
        if (message is IPlatformTrackableBusMessage trackableBusMessage) UserContextAccessor.Current.UpsertMany(trackableBusMessage.RequestContext);

        if (InboxBusMessageRepo != null && !IsInstanceExecutingFromInboxHelper)
        {
            await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                RootServiceProvider,
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

    public ILogger CreateGlobalLogger()
    {
        return CreateLogger(RootServiceProvider.GetRequiredService<ILoggerFactory>());
    }
}
