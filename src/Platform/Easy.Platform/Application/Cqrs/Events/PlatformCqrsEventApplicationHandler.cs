using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Events.InboxSupport;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public interface IPlatformCqrsEventApplicationHandler : IPlatformCqrsEventHandler
{
    bool IsCurrentInstanceCalledFromInboxBusMessageConsumer { get; set; }

    public bool EnableInboxEventBusMessage { get; }

    Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken);

    Task Handle(object notification, CancellationToken cancellationToken);

    bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, object @event);
}

public interface IPlatformCqrsEventApplicationHandler<in TEvent> : IPlatformCqrsEventApplicationHandler, IPlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken);

    bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, TEvent @event);
}

public abstract class PlatformCqrsEventApplicationHandler<TEvent> : PlatformCqrsEventHandler<TEvent>, IPlatformCqrsEventApplicationHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider) : base(loggerFactory)
    {
        UnitOfWorkManager = unitOfWorkManager;
        ServiceProvider = serviceProvider;
        IsInjectingUserContextAccessor = GetType().IsUsingGivenTypeViaConstructor<IPlatformApplicationUserContextAccessor>();
        IsInjectingApplicationBusMessageProducer = GetType().IsUsingGivenTypeViaConstructor<IPlatformApplicationBusMessageProducer>();
    }

    protected virtual bool AutoOpenUow => false;

    public bool IsInjectingUserContextAccessor { get; }

    protected bool IsInjectingApplicationBusMessageProducer { get; }

    /// <summary>
    /// Default return True. When True, Support for store cqrs event handler as inbox if inbox bus message is enabled in persistence module
    /// </summary>
    public virtual bool EnableInboxEventBusMessage => true;

    public bool IsCurrentInstanceCalledFromInboxBusMessageConsumer { get; set; }

    public Task ExecuteHandleAsync(object @event, CancellationToken cancellationToken)
    {
        return DoExecuteHandleAsync(@event.As<TEvent>(), cancellationToken);
    }

    public Task Handle(object notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification.As<TEvent>(), cancellationToken);
    }

    public bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, object @event)
    {
        return CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, @event.As<TEvent>());
    }

    public override Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        return DoHandle(notification, cancellationToken);
    }

    public override async Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        await DoExecuteHandleAsync(@event, cancellationToken);
    }

    public virtual bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, TEvent @event)
    {
        // EventHandler using IPlatformApplicationUserContextAccessor cannot use inbox because user request context is not available when process inbox message
        var usingApplicationUserContextAccessor = IsInjectingUserContextAccessor;
        var hasEnabledInboxFeature = EnableInboxEventBusMessage && hasInboxMessageRepository;

        if (usingApplicationUserContextAccessor && hasEnabledInboxFeature)
            CreateLogger(LoggerFactory)
                .LogWarning(
                    "[WARNING] Auto handing event directly, not support using InboxEvent in [EventHandlerType:{EventHandlerType}]. " +
                    "EventHandler using IPlatformApplicationUserContextAccessor cannot use inbox because user request context is not available when process inbox message. " +
                    "Should refactor removing using IPlatformApplicationUserContextAccessor to support inbox.",
                    GetType().FullName);

        return hasEnabledInboxFeature && !usingApplicationUserContextAccessor;
    }

    protected override bool AllowHandleInBackgroundThread(TEvent notification)
    {
        var eventSourceUow = TryGetEventCurrentActiveSourceUow(notification);
        var hasInboxMessageRepository = PlatformGlobal.RootServiceProvider
            .CheckHasRegisteredScopedService<IPlatformInboxBusMessageRepository>();

        return !IsInjectingApplicationBusMessageProducer && eventSourceUow == null && !CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, notification);
    }

    protected override Dictionary<string, object> BuildDataContextBeforeNewScopeExecution()
    {
        if (IsInjectingUserContextAccessor)
        {
            // Need to get current user context outside of QueueActionInBackground to has data because it read current context by thread. If inside => other threads => lose identity data
            var currentUserContextAllValues =
                PlatformGlobal.RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>().Current.GetAllKeyValues();

            return currentUserContextAllValues;
        }

        return null;
    }

    protected override void ReApplyDataContextInNewScopeExecution(
        IServiceProvider newScopeServiceProvider,
        Dictionary<string, object> dataContextBeforeNewScopeExecution)
    {
        // Need to get set back current user context outside of QueueActionInBackground into the current thread context
        if (IsInjectingUserContextAccessor && dataContextBeforeNewScopeExecution != null)
            newScopeServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>().Current.SetValues(dataContextBeforeNewScopeExecution);
    }

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

    protected virtual async Task DoHandle(TEvent notification, CancellationToken cancellationToken)
    {
        if (!HandleWhen(notification)) return;

        var eventSourceUow = TryGetEventCurrentActiveSourceUow(notification);
        var hasInboxMessageRepository = PlatformGlobal.RootServiceProvider
            .CheckHasRegisteredScopedService<IPlatformInboxBusMessageRepository>();

        if (eventSourceUow != null &&
            !CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, notification) &&
            !notification.HasForceWaitEventHandler(GetType()))
        {
            // Need to get current data context outside of DoExecuteInstanceInNewScope to has data because it read current context by thread. If inside => other threads => lose identity data
            var currentDataContextAllValues = BuildDataContextBeforeNewScopeExecution();

            eventSourceUow.OnCompletedActions.Add(
                async () => await DoExecuteInstanceInNewScope(notification, currentDataContextAllValues));
        }
        else
        {
            await base.Handle(notification, cancellationToken);
        }
    }

    protected virtual async Task DoExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        var hasInboxMessageRepository = PlatformGlobal.RootServiceProvider
            .CheckHasRegisteredScopedService<IPlatformInboxBusMessageRepository>();

        if (CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, @event) &&
            !IsCurrentInstanceCalledFromInboxBusMessageConsumer &&
            !IsInjectingUserContextAccessor)
        {
            var eventSourceUow = TryGetEventCurrentActiveSourceUow(@event);
            var currentBusMessageIdentity =
                BuildCurrentBusMessageIdentity(PlatformGlobal.RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>());

            if (@event is IPlatformUowEvent && eventSourceUow != null)
            {
                var consumerInstance = ServiceProvider.GetRequiredService<PlatformCqrsEventInboxBusMessageConsumer>();
                var applicationSettingContext = ServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();

                await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                    serviceProvider: ServiceProvider,
                    consumer: consumerInstance,
                    inboxBusMessageRepository: ServiceProvider.GetRequiredService<IPlatformInboxBusMessageRepository>(),
                    message: CqrsEventInboxBusMessage(@event, eventHandlerType: GetType(), applicationSettingContext, currentBusMessageIdentity),
                    routingKey: PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(typeof(TEvent), applicationSettingContext.ApplicationName),
                    loggerFactory: CreateGlobalLogger,
                    retryProcessFailedMessageInSecondsUnit: PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                    allowProcessInBackgroundThread: AllowHandleInBackgroundThread(@event),
                    handleDirectlyExistingInboxMessage: null,
                    handleInUow: eventSourceUow,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await PlatformGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                    async (
                        IServiceProvider serviceProvider,
                        IPlatformInboxBusMessageRepository inboxMessageRepository,
                        IPlatformApplicationSettingContext applicationSettingContext) =>
                    {
                        var consumerInstance = serviceProvider.GetRequiredService<PlatformCqrsEventInboxBusMessageConsumer>();

                        await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(
                            serviceProvider: serviceProvider,
                            consumer: consumerInstance,
                            inboxBusMessageRepository: inboxMessageRepository,
                            message: CqrsEventInboxBusMessage(@event, eventHandlerType: GetType(), applicationSettingContext, currentBusMessageIdentity),
                            routingKey: PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(typeof(TEvent), applicationSettingContext.ApplicationName),
                            loggerFactory: CreateGlobalLogger,
                            retryProcessFailedMessageInSecondsUnit: PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit,
                            allowProcessInBackgroundThread: AllowHandleInBackgroundThread(@event),
                            handleDirectlyExistingInboxMessage: null,
                            handleInUow: null,
                            cancellationToken: cancellationToken);
                    });
            }
        }
        else
        {
            if (AutoOpenUow)
            {
                // If handler already executed in background or from inbox consumer, not need to open new scope for open uow
                // If not then create new scope to open new uow so that multiple events handlers from an event do not get conflicted
                // uow in the same scope if not open new scope
                if (AllowHandleInBackgroundThread(@event) || CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, @event))
                    using (var uow = UnitOfWorkManager.Begin())
                    {
                        await HandleAsync(@event, cancellationToken);
                        await uow.CompleteAsync(cancellationToken);
                    }
                else
                    using (var newScope = PlatformGlobal.RootServiceProvider.CreateScope())
                    {
                        using (var uow = newScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin())
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
        }
    }

    protected virtual PlatformBusMessage<PlatformCqrsEventBusMessagePayload> CqrsEventInboxBusMessage(
        TEvent @event,
        Type eventHandlerType,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformBusMessageIdentity currentBusMessageIdentity)
    {
        return PlatformBusMessage<PlatformCqrsEventBusMessagePayload>.New<PlatformBusMessage<PlatformCqrsEventBusMessagePayload>>(
            trackId: $"{@event.Id}-{eventHandlerType.Name}",
            payload: PlatformCqrsEventBusMessagePayload.New(@event, eventHandlerType.FullName),
            identity: currentBusMessageIdentity,
            producerContext: applicationSettingContext.ApplicationName,
            messageGroup: nameof(PlatformCqrsEvent),
            messageAction: @event.EventAction);
    }

    protected IUnitOfWork TryGetEventCurrentActiveSourceUow(TEvent notification)
    {
        return notification.As<IPlatformUowEvent>().SourceUowId != null
            ? UnitOfWorkManager.TryGetCurrentOrCreatedActiveUow(notification.As<IPlatformUowEvent>().SourceUowId)
            : UnitOfWorkManager.TryGetCurrentActiveUow();
    }

    public virtual PlatformBusMessageIdentity BuildCurrentBusMessageIdentity(IPlatformApplicationUserContextAccessor userContextAccessor)
    {
        return new PlatformBusMessageIdentity
        {
            UserId = userContextAccessor.Current.UserId(),
            RequestId = userContextAccessor.Current.RequestId(),
            UserName = userContextAccessor.Current.UserName()
        };
    }

    public static ILogger CreateGlobalLogger()
    {
        return CreateLogger(PlatformGlobal.LoggerFactory);
    }
}
