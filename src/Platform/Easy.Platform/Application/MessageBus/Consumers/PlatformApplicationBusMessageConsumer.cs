using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers;

/// <summary>
/// Represents a message bus consumer for platform applications.
/// This interface extends the <see cref="IPlatformMessageBusConsumer" /> interface with additional properties and methods
/// specific to handling inbox messages.
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
    /// Gets or sets a value indicating whether if you need to check any consumer other previous not processed inbox message before processing. Default is true
    /// </summary>
    public bool NeedToCheckAnySameConsumerOtherPreviousNotProcessedInboxMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is called to execute handling logic for an inbox message.
    /// </summary>
    public bool IsHandlingLogicForInboxMessage { get; set; }

    public bool HasErrorAndShouldNeverRetry { get; set; }
}

/// <summary>
/// Represents a message bus consumer for platform applications that handles messages of a specific type.
/// This interface extends the <see cref="IPlatformMessageBusConsumer{TMessage}" /> and <see cref="IPlatformApplicationMessageBusConsumer" /> interfaces.
/// </summary>
/// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
public interface IPlatformApplicationMessageBusConsumer<in TMessage> : IPlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer
    where TMessage : class, new()
{
}

/// <summary>
/// An abstract base class for platform application message bus consumers that handle messages of a specific type.
/// This class provides common functionality for handling inbox messages and managing units of work.
/// </summary>
/// <typeparam name="TMessage">The type of the message being consumed.</typeparam>
public abstract class PlatformApplicationMessageBusConsumer<TMessage> : PlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
    /// <summary>
    /// A lazy-initialized instance of the inbox bus message repository.
    /// </summary>
    protected readonly Lazy<IPlatformInboxBusMessageRepository> InboxBusMessageRepo;

    /// <summary>
    /// The configuration for the inbox pattern.
    /// </summary>
    protected readonly PlatformInboxConfig InboxConfig;

    /// <summary>
    /// The root service provider.
    /// </summary>
    protected readonly IPlatformRootServiceProvider RootServiceProvider;

    /// <summary>
    /// The service provider for the current scope.
    /// </summary>
    protected readonly IServiceProvider ServiceProvider;

    /// <summary>
    /// The unit of work manager.
    /// </summary>
    protected readonly IPlatformUnitOfWorkManager UowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformApplicationMessageBusConsumer{TMessage}" /> class.
    /// </summary>
    /// <param name="loggerFactory">A factory for creating loggers.</param>
    /// <param name="uowManager">The unit of work manager.</param>
    /// <param name="serviceProvider">The service provider for the current scope.</param>
    /// <param name="rootServiceProvider">The root service provider.</param>
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

    /// <summary>
    /// Gets a value indicating whether to automatically open a unit of work when handling a message.
    /// </summary>
    public virtual bool AutoOpenUow => true;

    /// <summary>
    /// Gets the request context accessor.
    /// </summary>
    protected IPlatformApplicationRequestContextAccessor RequestContextAccessor { get; }

    /// <summary>
    /// Gets the application setting context.
    /// </summary>
    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    /// <summary>
    /// Gets a value indicating whether to allow the use of inbox messages. Default is True. Set to False to consume messages directly without using the inbox.
    /// </summary>
    public virtual bool AllowUseInboxMessage => true;

    public bool AllowHandleNewInboxMessageInBackground { get; set; } = false;

    public virtual int MinRowVersionConflictRetryOnFailedTimes { get; set; } = Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent;

    /// <inheritdoc />
    public bool NeedToCheckAnySameConsumerOtherPreviousNotProcessedInboxMessage { get; set; } = true;

    /// <inheritdoc />
    public PlatformInboxBusMessage HandleExistingInboxMessage { get; set; }

    /// <inheritdoc />
    public bool AutoDeleteProcessedInboxEventMessageImmediately { get; set; }

    /// <inheritdoc />
    public bool IsHandlingLogicForInboxMessage { get; set; }

    public bool HasErrorAndShouldNeverRetry { get; set; }

    /// <summary>
    /// Executes the message handling logic, either using the inbox pattern or directly.
    /// </summary>
    /// <param name="message">The message being consumed.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public override async Task ExecuteHandleLogicAsync(TMessage message, string routingKey)
    {
        // If the inbox pattern is enabled and allowed, handle the message using the inbox pattern.
        if (InboxBusMessageRepo != null && AllowUseInboxMessage && !IsHandlingLogicForInboxMessage)
        {
            // if message can handle parallel without check in order sub queue then can try to execute immediately
            if (message.As<IPlatformSubMessageQueuePrefixSupport>()?.SubQueuePrefix().IsNullOrEmpty() == true)
                try
                {
                    // Try to execute directly to improve performance. Then if failed execute use inbox to support retry failed message later.
                    await HandleMessageDirectly(message, routingKey);
                }
                catch (Exception)
                {
                    await HandleExecutingInboxConsumerAsync(message, routingKey);
                }
            else
                await HandleExecutingInboxConsumerAsync(message, routingKey);
        }
        // Otherwise, handle the message directly.
        else
        {
            await HandleMessageDirectly(message, routingKey);
        }
    }

    private async Task HandleExecutingInboxConsumerAsync(TMessage message, string routingKey)
    {
        await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
            RootServiceProvider,
            ServiceProvider,
            consumerType: GetType(),
            inboxBusMessageRepository: InboxBusMessageRepo.Value,
            inboxConfig: InboxConfig,
            applicationSettingContext: ApplicationSettingContext,
            message: message,
            forApplicationName: ApplicationSettingContext.ApplicationName,
            routingKey: routingKey,
            loggerFactory: CreateLogger,
            retryProcessFailedMessageInSecondsUnit: InboxConfig.RetryProcessFailedMessageInSecondsUnit,
            handleExistingInboxMessage: HandleExistingInboxMessage,
            handleExistingInboxMessageConsumerInstance: this,
            subQueueMessageIdPrefix: message.As<IPlatformSubMessageQueuePrefixSupport>()?.SubQueuePrefix(),
            autoDeleteProcessedMessageImmediately: AutoDeleteProcessedInboxEventMessageImmediately,
            needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage: NeedToCheckAnySameConsumerOtherPreviousNotProcessedInboxMessage,
            handleInUow: null,
            allowHandleNewInboxMessageInBackground: AllowHandleNewInboxMessageInBackground);
    }

    private async Task HandleMessageDirectly(TMessage message, string routingKey)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync<PlatformDomainRowVersionConflictException>(
            async () =>
            {
                try
                {
                    // Update the request context with information from the message.
                    if (message is IPlatformTrackableBusMessage trackableBusMessage) RequestContextAccessor.Current.UpsertMany(trackableBusMessage.RequestContext);

                    // If auto-opening a unit of work is enabled, handle the message within a unit of work.
                    if (AutoOpenUow)
                    {
                        using (var uow = UowManager.Begin())
                        {
                            await HandleLogicAsync(message, routingKey);
                            await uow.CompleteAsync();
                        }
                    }
                    // Otherwise, handle the message without a unit of work.
                    else
                    {
                        await HandleLogicAsync(message, routingKey);

                        // If a unit of work was started elsewhere, complete it to ensure changes are saved.
                        if (UowManager.HasCurrentActiveUow())
                            await UowManager.CurrentActiveUow().CompleteAsync();
                    }
                }
                finally
                {
                    // If garbage collection is enabled, perform garbage collection.
                    if (ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessage)
                        await Util.GarbageCollector.Collect(ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds);
                }
            },
            retryCount: MinRowVersionConflictRetryOnFailedTimes + RetryOnFailedTimes,
            sleepDurationProvider: p => TimeSpan.Zero);
    }
}
