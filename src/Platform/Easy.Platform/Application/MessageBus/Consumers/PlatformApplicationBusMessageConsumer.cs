using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers;

/// <summary>
/// Represents a message bus consumer for platform applications.
/// </summary>
public interface IPlatformApplicationMessageBusConsumer : IPlatformMessageBusConsumer
{
    /// <summary>
    /// Gets or sets the message to be handled if it exists in the inbox.
    /// </summary>
    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the processed inbox event message should be automatically deleted.
    /// </summary>
    public bool AutoDeleteProcessedInboxEventMessageImmediately { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is called to execute handling logic for an inbox message.
    /// </summary>
    public bool IsHandlingLogicForInboxMessage { get; set; }

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
    protected readonly Lazy<IPlatformInboxBusMessageRepository> InboxBusMessageRepo;
    protected readonly PlatformInboxConfig InboxConfig;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IPlatformUnitOfWorkManager UowManager;

    protected PlatformApplicationMessageBusConsumer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory)
    {
        UowManager = uowManager;
        ServiceProvider = serviceProvider;
        RootServiceProvider = rootServiceProvider;
        InboxBusMessageRepo = new Lazy<IPlatformInboxBusMessageRepository>(serviceProvider.GetService<IPlatformInboxBusMessageRepository>);
        InboxConfig = serviceProvider.GetRequiredService<PlatformInboxConfig>();
        RequestContextAccessor = ServiceProvider.GetRequiredService<IPlatformApplicationRequestContextAccessor>();
        ApplicationSettingContext = rootServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();
    }

    public virtual bool AutoBeginUow => false;

    protected IPlatformApplicationRequestContextAccessor RequestContextAccessor { get; }

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    /// <summary>
    /// Default is True. Set to False to not allow use inbox message, just consume directly.
    /// </summary>
    public virtual bool AllowUseInboxMessage => true;

    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }
    public bool AutoDeleteProcessedInboxEventMessageImmediately { get; set; }
    public bool IsHandlingLogicForInboxMessage { get; set; }
    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }
    public TimeSpan? InboxProcessingMaxTimeout { get; set; }

    public override async Task ExecuteHandleLogicAsync(TMessage message, string routingKey)
    {
        if (InboxBusMessageRepo != null && AllowUseInboxMessage && !IsHandlingLogicForInboxMessage)
            await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                RootServiceProvider,
                ServiceProvider,
                consumerType: GetType(),
                inboxBusMessageRepository: InboxBusMessageRepo.Value,
                inboxConfig: InboxConfig,
                message: message,
                routingKey: routingKey,
                loggerFactory: CreateGlobalLogger,
                retryProcessFailedMessageInSecondsUnit: InboxConfig.RetryProcessFailedMessageInSecondsUnit,
                allowProcessInBackgroundThread: AllowProcessInboxMessageInBackgroundThread,
                handleExistingInboxMessage: HandleExistingInboxMessage,
                handleExistingInboxMessageConsumerInstance: this,
                extendedMessageIdPrefix: message.As<IPlatformSubMessageQueuePrefixSupport>()?.SubQueuePrefix(),
                autoDeleteProcessedMessageImmediately: AutoDeleteProcessedInboxEventMessageImmediately,
                handleInUow: null);
        else
            try
            {
                if (message is IPlatformTrackableBusMessage trackableBusMessage) RequestContextAccessor.Current.UpsertMany(trackableBusMessage.RequestContext);

                if (AutoBeginUow)
                {
                    using (var uow = UowManager.Begin())
                    {
                        await HandleLogicAsync(message, routingKey);
                        await uow.CompleteAsync();
                    }
                }
                else
                {
                    await HandleLogicAsync(message, routingKey);

                    // Support if legacy app begin uow somewhere without complete it
                    // Auto complete current active uow if any to save database.
                    if (UowManager.HasCurrentActiveUow())
                        await UowManager.CurrentActiveUow().CompleteAsync();
                }
            }
            finally
            {
                if (ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessage)
                    Util.GarbageCollector.Collect(ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds);
            }
    }

    public ILogger CreateGlobalLogger()
    {
        return CreateLogger(RootServiceProvider.GetRequiredService<ILoggerFactory>());
    }
}
