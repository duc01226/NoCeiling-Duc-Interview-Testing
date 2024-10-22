using Easy.Platform.Application.Cqrs.Events.InboxSupport;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

/// <summary>
/// Defines the contract for a Platform CQRS Event Application Handler.
/// </summary>
/// <remarks>
/// This interface extends the IPlatformCqrsEventHandler and provides additional properties and methods
/// for handling CQRS events in the application context.
/// </remarks>
public interface IPlatformCqrsEventApplicationHandler : IPlatformCqrsEventHandler
{
    /// <summary>
    /// Gets a value indicating whether to enable Inbox Event Bus Message.
    /// </summary>
    public bool EnableInboxEventBusMessage { get; }

    public bool IsCurrentInstanceCalledFromInboxBusMessageConsumer { get; set; }

    /// <summary>
    /// Determines whether the event can be handled using Inbox Consumer.
    /// </summary>
    /// <param name="event">The event to check.</param>
    bool CanExecuteHandlingEventUsingInboxConsumer(object @event);
}

public interface IPlatformCqrsEventApplicationHandler<in TEvent> : IPlatformCqrsEventApplicationHandler, IPlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    bool CanExecuteHandlingEventUsingInboxConsumer(TEvent @event);
}

public abstract class PlatformCqrsEventApplicationHandler<TEvent> : PlatformCqrsEventHandler<TEvent>, IPlatformCqrsEventApplicationHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IPlatformUnitOfWorkManager UnitOfWorkManager;

    private readonly IPlatformApplicationRequestContextAccessor requestContextAccessor;

    private double retryOnFailedDelaySeconds = Util.TaskRunner.DefaultResilientDelaySeconds;
    private int retryOnFailedTimes = Util.TaskRunner.DefaultResilientRetryCount;
    private bool? throwExceptionOnHandleFailed;

    public PlatformCqrsEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, rootServiceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
        ServiceProvider = serviceProvider;
        requestContextAccessor = ServiceProvider.GetRequiredService<IPlatformApplicationRequestContextAccessor>();
        ApplicationSettingContext = ServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();
        Logger = new Lazy<ILogger>(() => CreateLogger(LoggerFactory));
    }

    public override double RetryOnFailedDelaySeconds
    {
        get => !HasInboxMessageSupport() && !MustWaitHandlerExecutionFinishedImmediately ? retryOnFailedDelaySeconds * 10 : retryOnFailedDelaySeconds;
        set => retryOnFailedDelaySeconds = value;
    }

    protected virtual bool AutoOpenUow => true;

    protected Lazy<ILogger> Logger { get; }

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    public virtual bool AutoDeleteProcessedInboxEventMessage => false;

    public int RetryEventInboxBusMessageConsumerOnFailedDelaySeconds { get; set; } = 1;

    public int RetryEventInboxBusMessageConsumerMaxCount { get; set; } = 3;

    public override int RetryOnFailedTimes
    {
        get => !HasInboxMessageSupport() && !MustWaitHandlerExecutionFinishedImmediately ? retryOnFailedTimes * 100 : retryOnFailedTimes;
        set => retryOnFailedTimes = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the current instance of the event handler is called from the Inbox Bus Message Consumer.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is called from the Inbox Bus Message Consumer; otherwise, <c>false</c>.
    /// </value>
    public bool IsCurrentInstanceCalledFromInboxBusMessageConsumer { get; set; }

    /// <summary>
    /// Default return False. When True, Support for store cqrs event handler as inbox if inbox bus message is enabled in persistence module
    /// </summary>
    public virtual bool EnableInboxEventBusMessage => true;

    public override bool ThrowExceptionOnHandleFailed
    {
        get => throwExceptionOnHandleFailed ?? IsCurrentInstanceCalledFromInboxBusMessageConsumer;
        set => throwExceptionOnHandleFailed = value;
    }

    public override Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken)
    {
        return ExecuteHandleWithTracingAsync(@event.As<TEvent>(), () => DoExecuteHandleAsync(@event.As<TEvent>(), cancellationToken));
    }

    public override Task Handle(object @event, CancellationToken cancellationToken)
    {
        return DoHandle(@event.As<TEvent>(), cancellationToken);
    }

    public override async Task<bool> HandleWhen(object @event)
    {
        return await HandleWhen(@event.As<TEvent>());
    }

    public bool CanExecuteHandlingEventUsingInboxConsumer(object @event)
    {
        return CanExecuteHandlingEventUsingInboxConsumer(@event.As<TEvent>());
    }

    public override async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        await DoHandle(notification, cancellationToken);
    }

    public override Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        return ExecuteHandleWithTracingAsync(@event, () => DoExecuteHandleAsync(@event, cancellationToken));
    }

    /// <summary>
    /// Determines whether the event can be handled using the Inbox Consumer.
    /// </summary>
    /// <param name="event">The event to be handled.</param>
    /// <returns>
    /// Returns true if the Inbox Feature is enabled, the handler is not using the IPlatformApplicationRequestContextAccessor,
    /// and the event does not require immediate execution. Otherwise, it returns false.
    /// </returns>
    /// <remarks>
    /// Event handlers using IPlatformApplicationRequestContextAccessor cannot use the inbox because the user request context is not available when processing inbox messages.
    /// </remarks>
    public virtual bool CanExecuteHandlingEventUsingInboxConsumer(TEvent @event)
    {
        return HasInboxMessageSupport() && NotNeedWaitHandlerExecutionFinishedImmediately(@event);
    }

    /// <summary>
    /// Determines whether the event handling for the specified event can be executed in a background thread.
    /// </summary>
    /// <param name="event">The event to be handled.</param>
    /// <returns>
    /// Returns <c>true</c> if the event handling can be executed in a background thread; otherwise, <c>false</c>.
    /// This method returns <c>false</c> if any of the following conditions are met:
    /// - The current active Unit of Work (UoW) for the event is not null.
    /// - The event requires immediate execution of its handlers.
    /// - The event handling is forced to be executed in the same UoW as the event trigger.
    /// </returns>
    /// <remarks>
    /// This method is used to decide whether the event handling can be offloaded to a background thread for better performance and non-blocking operation.
    /// </remarks>
    protected override bool AllowHandleInBackgroundThread(TEvent @event)
    {
        return TryGetCurrentOrCreatedActiveUow(@event).Pipe(p => p == null || p.IsPseudoTransactionUow()) &&
               NotNeedWaitHandlerExecutionFinishedImmediately(@event) &&
               !ForceCurrentInstanceHandleInCurrentThread;
    }

    /// <summary>
    /// Copies properties from the previous instance of the event handler to the new instance before execution.
    /// </summary>
    /// <param name="previousInstance">The previous instance of the event handler.</param>
    /// <param name="newInstance">The new instance of the event handler.</param>
    /// <remarks>
    /// This method is used to ensure that the new instance of the event handler has the same state as the previous instance before execution.
    /// Specifically, it copies the value of the `IsCurrentInstanceCalledFromInboxBusMessageConsumer` property from the previous instance to the new instance.
    /// </remarks>
    protected override void CopyPropertiesToNewInstanceBeforeExecution(
        PlatformCqrsEventHandler<TEvent> previousInstance,
        PlatformCqrsEventHandler<TEvent> newInstance)
    {
        base.CopyPropertiesToNewInstanceBeforeExecution(previousInstance, newInstance);

        var applicationHandlerPreviousInstance = previousInstance.As<PlatformCqrsEventApplicationHandler<TEvent>>();
        var applicationHandlerNewInstance = newInstance.As<PlatformCqrsEventApplicationHandler<TEvent>>();

        applicationHandlerNewInstance.IsCurrentInstanceCalledFromInboxBusMessageConsumer =
            applicationHandlerPreviousInstance.IsCurrentInstanceCalledFromInboxBusMessageConsumer;
    }

    /// <summary>
    /// Handles the specified event asynchronously.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <param name="couldRunInBackgroundThread">A boolean value indicating whether the handling could run in a background thread.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// If the RequestContext of the event is null or empty, it sets the RequestContext values from the RequestContextAccessor.
    /// If the event passes the HandleWhen condition, it tries to get the current active source unit of work of the event.
    /// If the conditions are met, it adds the DoExecuteInstanceInNewScope action to the OnSaveChangesCompletedActions of the unit of work.
    /// Otherwise, it calls the base DoHandle method.
    /// </remarks>
    protected override async Task DoHandle(TEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            if (@event.RequestContext == null || @event.RequestContext.IsEmpty())
                @event.SetRequestContextValues(requestContextAccessor.Current.GetAllKeyValues(ApplicationSettingContext.GetIgnoreRequestContextKeys()));

            if (RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.DistributedTracingStackTraceEnabled() == true &&
                @event.StackTrace == null)
                @event.StackTrace = PlatformEnvironment.StackTrace();

            var eventSourceUow = TryGetCurrentOrCreatedActiveUow(@event);

            if (eventSourceUow?.IsPseudoTransactionUow() == false &&
                NotNeedWaitHandlerExecutionFinishedImmediately(@event))
                eventSourceUow.OnSaveChangesCompletedActions.Add(
                    async () =>
                    {
                        // Execute task in background separated thread task
                        _ = ExecuteHandleInNewScopeAsync(@event, cancellationToken);
                    });
            else
                await base.DoHandle(@event, cancellationToken);
        }
        catch (Exception e)
        {
            if (ThrowExceptionOnHandleFailed) throw;
            LogError(@event, e.BeautifyStackTrace(), LoggerFactory);
        }
    }

    /// <summary>
    /// Executes the event handling asynchronously.
    /// </summary>
    /// <param name="event">The event of type TEvent to be handled.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <remarks>
    /// This method checks if the event can be handled in a background thread and if so, sets the user context values.
    /// It also checks if the event can be handled using an inbox consumer and if not currently called from an inbox bus message consumer,
    /// and if the event does not require immediate execution, it processes the event accordingly.
    /// If the event cannot be handled using an inbox consumer, it checks if a unit of work should be automatically opened and handles the event accordingly.
    /// </remarks>
    /// <returns>A Task representing the asynchronous operation.</returns>
    protected virtual async Task DoExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            if (AllowHandleInBackgroundThread(@event) && @event.RequestContext?.Any() == true)
                requestContextAccessor.Current.SetValues(@event.RequestContext);

            if (CanExecuteHandlingEventUsingInboxConsumer(@event) &&
                !IsCurrentInstanceCalledFromInboxBusMessageConsumer &&
                NotNeedWaitHandlerExecutionFinishedImmediately(@event))
                // Try to execute directly once to enhance performance, if failed then try use inbox
                try
                {
                    await RunHandleAsync(@event, cancellationToken, retryCount: 2);
                }
                catch (Exception)
                {
                    var eventSourceUow = TryGetCurrentOrCreatedActiveUow(@event);
                    var currentBusMessageIdentity = BuildCurrentBusMessageIdentity(@event.RequestContext);

                    if (@event is IPlatformUowEvent && eventSourceUow != null && !eventSourceUow.IsPseudoTransactionUow())
                        await HandleExecutingInboxConsumerAsync(
                            @event,
                            ServiceProvider,
                            ServiceProvider.GetRequiredService<PlatformInboxConfig>(),
                            ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>(),
                            ServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>(),
                            currentBusMessageIdentity,
                            eventSourceUow,
                            cancellationToken);
                    else
                        await RootServiceProvider.ExecuteInjectScopedAsync(
                            async (
                                IServiceProvider serviceProvider,
                                PlatformInboxConfig inboxConfig,
                                IPlatformInboxBusMessageRepository inboxMessageRepository,
                                IPlatformApplicationSettingContext applicationSettingContext) =>
                            {
                                await HandleExecutingInboxConsumerAsync(
                                    @event,
                                    serviceProvider,
                                    inboxConfig,
                                    inboxMessageRepository,
                                    applicationSettingContext,
                                    currentBusMessageIdentity,
                                    null,
                                    cancellationToken);
                            });
                }
            else
                await RunHandleAsync(@event, cancellationToken);
        }
        finally
        {
            ApplicationSettingContext.ProcessAutoGarbageCollect();
        }
    }

    protected async Task RunHandleAsync(TEvent @event, CancellationToken cancellationToken, int? retryCount = null)
    {
        if (!await HandleWhen(@event)) return;

        if (ApplicationSettingContext.IsDebugInformationMode)
            Logger.Value.LogInformation("{Type} {Method} STARTED", GetType().FullName, nameof(RunHandleAsync));

        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync<PlatformDomainRowVersionConflictException>(
            async () =>
            {
                if (AutoOpenUow)
                {
                    // If handler already executed in background or from inbox consumer, not need to open new scope for open uow
                    // If not then create new scope to open new uow so that multiple events handlers from an event do not get conflicted
                    // uow in the same scope if not open new scope
                    if (AllowHandleInBackgroundThread(@event) || CanExecuteHandlingEventUsingInboxConsumer(@event))
                        using (var uow = UnitOfWorkManager.Begin())
                        {
                            await HandleAsync(@event, cancellationToken);
                            await uow.CompleteAsync(cancellationToken);
                        }
                    else
                        using (var newScope = RootServiceProvider.CreateScope())
                        {
                            using (var uow = newScope.ServiceProvider.GetRequiredService<IPlatformUnitOfWorkManager>().Begin())
                            {
                                await newScope.ServiceProvider.GetRequiredService(GetType())
                                    .As<PlatformCqrsEventApplicationHandler<TEvent>>()
                                    .With(newInstance => CopyPropertiesToNewInstanceBeforeExecution(this, newInstance))
                                    .HandleAsync(@event, cancellationToken);

                                await uow.CompleteAsync(cancellationToken);
                            }
                        }
                }
                else
                {
                    await HandleAsync(@event, cancellationToken);
                }
            },
            retryCount: retryCount ?? RetryOnFailedTimes,
            sleepDurationProvider: p => RetryOnFailedDelaySeconds.Seconds(),
            cancellationToken: cancellationToken);

        if (ApplicationSettingContext.IsDebugInformationMode)
            Logger.Value.LogInformation("{Type} {Method} FINISHED", GetType().FullName, nameof(RunHandleAsync));
    }

    protected bool HasInboxMessageSupport()
    {
        return RootServiceProvider.CheckHasRegisteredScopedService<IPlatformInboxBusMessageRepository>() && EnableInboxEventBusMessage;
    }

    protected async Task HandleExecutingInboxConsumerAsync(
        TEvent @event,
        IServiceProvider serviceProvider,
        PlatformInboxConfig inboxConfig,
        IPlatformInboxBusMessageRepository inboxMessageRepository,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformBusMessageIdentity currentBusMessageIdentity,
        IPlatformUnitOfWork eventSourceUow,
        CancellationToken cancellationToken)
    {
        var eventSubQueuePrefix = @event.As<IPlatformSubMessageQueuePrefixSupport>()?.SubQueuePrefix();

        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () => await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                rootServiceProvider: RootServiceProvider,
                currentScopeServiceProvider: serviceProvider,
                consumerType: typeof(PlatformCqrsEventInboxBusMessageConsumer),
                inboxBusMessageRepository: inboxMessageRepository,
                inboxConfig: inboxConfig,
                applicationSettingContext: applicationSettingContext,
                message: CqrsEventInboxBusMessage(@event, eventHandlerType: GetType(), applicationSettingContext, currentBusMessageIdentity),
                forApplicationName: ApplicationSettingContext.ApplicationName,
                routingKey: PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(typeof(TEvent), applicationSettingContext.ApplicationName),
                loggerFactory: CreateGlobalLogger,
                retryProcessFailedMessageInSecondsUnit: PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                handleExistingInboxMessage: null,
                currentScopeConsumerInstance: null,
                handleInUow: eventSourceUow,
                autoDeleteProcessedMessageImmediately: AutoDeleteProcessedInboxEventMessage &&
                                                       RootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled != true,
                subQueueMessageIdPrefix:
                $"{GetType().GetNameOrGenericTypeName()}-{eventSubQueuePrefix.Pipe(p => p.IsNullOrEmpty() ? $"NoSubQueueRandomId-{Ulid.NewUlid()}" : p)}",
                needToCheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessage: eventSubQueuePrefix.IsNotNullOrEmpty(),
                allowHandleNewInboxMessageInBackground: true,
                cancellationToken: cancellationToken),
            retryCount: RetryEventInboxBusMessageConsumerMaxCount,
            sleepDurationProvider: retryAttempt => RetryEventInboxBusMessageConsumerOnFailedDelaySeconds.Seconds(),
            cancellationToken: cancellationToken,
            onRetry: (exception, retryTime, retryAttempt, context) => Logger.Value.LogError(
                exception.BeautifyStackTrace(),
                "Execute inbox consumer for EventType:{EventType}; EventContent:{EventContent}.",
                @event.GetType().FullName,
                @event.ToJson()));
    }

    protected virtual PlatformBusMessage<PlatformCqrsEventBusMessagePayload> CqrsEventInboxBusMessage(
        TEvent @event,
        Type eventHandlerType,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformBusMessageIdentity currentBusMessageIdentity)
    {
        return PlatformBusMessage<PlatformCqrsEventBusMessagePayload>.New<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>(
            trackId: @event.Id,
            payload: PlatformCqrsEventBusMessagePayload.New(@event, eventHandlerType.FullName),
            identity: currentBusMessageIdentity,
            producerContext: applicationSettingContext.ApplicationName,
            messageGroup: nameof(PlatformCqrsEvent),
            messageAction: @event.EventAction,
            requestContext: @event.RequestContext);
    }

    protected IPlatformUnitOfWork? TryGetCurrentOrCreatedActiveUow(TEvent notification)
    {
        if (notification.As<IPlatformUowEvent>() == null) return null;

        return UnitOfWorkManager.TryGetCurrentOrCreatedActiveUow(notification.As<IPlatformUowEvent>().SourceUowId);
    }

    public virtual PlatformBusMessageIdentity BuildCurrentBusMessageIdentity(IDictionary<string, object> eventRequestContext)
    {
        return new PlatformBusMessageIdentity
        {
            UserId = eventRequestContext.UserId(),
            RequestId = eventRequestContext.RequestId(),
            UserName = eventRequestContext.UserName()
        };
    }

    public ILogger CreateGlobalLogger()
    {
        return CreateLogger(RootServiceProvider.GetRequiredService<ILoggerFactory>());
    }
}
