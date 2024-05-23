using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public interface IPlatformCqrsEventBusMessageProducer
{
    public static Type GetTMessageArgumentType(Type cqrsEventBusMessageProducerType)
    {
        return cqrsEventBusMessageProducerType.GetGenericArguments()[1];
    }
}

/// <summary>
/// This interface is used for conventional register all PlatformCqrsEventBusProducer
/// </summary>
public interface IPlatformCqrsEventBusMessageProducer<in TEvent> : INotificationHandler<TEvent>, IPlatformCqrsEventBusMessageProducer
    where TEvent : PlatformCqrsEvent, new()
{
}

public abstract class PlatformCqrsEventBusMessageProducer<TEvent, TMessage>
    : PlatformCqrsEventApplicationHandler<TEvent>, IPlatformCqrsEventBusMessageProducer<TEvent>
    where TEvent : PlatformCqrsEvent, new()
    where TMessage : class, new()
{
    protected readonly IPlatformApplicationBusMessageProducer ApplicationBusMessageProducer;

    public PlatformCqrsEventBusMessageProducer(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer) : base(loggerFactory, unitOfWorkManager, serviceProvider, rootServiceProvider)
    {
        ApplicationBusMessageProducer = applicationBusMessageProducer;
    }

    public override bool EnableInboxEventBusMessage => false;

    protected override bool AutoOpenUow => false;

    protected override bool AllowUsingUserContextAccessor => true;

    protected override bool MustWaitHandlerExecutionFinishedImmediately => ApplicationBusMessageProducer.HasOutboxMessageSupport();

    protected abstract TMessage BuildMessage(TEvent @event);

    protected override bool AllowHandleInBackgroundThread(TEvent @event)
    {
        return base.AllowHandleInBackgroundThread(@event) && !ApplicationBusMessageProducer.HasOutboxMessageSupport();
    }

    protected override async Task HandleAsync(
        TEvent @event,
        CancellationToken cancellationToken)
    {
        await SendMessage(@event, cancellationToken);
    }

    public override void LogError(TEvent notification, Exception exception, ILoggerFactory loggerFactory)
    {
        CreateLogger(loggerFactory)
            .LogError(
                exception.BeautifyStackTrace(),
                "[PlatformCqrsEventBusMessageProducer] Failed to send {MessageName}. [[Error:{Error}]]. MessageContent: {MessageContent}. EventContent:{EventContent}",
                typeof(TMessage).FullName,
                exception.Message,
                exception.As<PlatformMessageBusException<TMessage>>()?.EventBusMessage.ToFormattedJson(),
                notification.ToFormattedJson());
    }

    protected virtual async Task SendMessage(TEvent @event, CancellationToken cancellationToken)
    {
        await ApplicationBusMessageProducer.SendAsync(
            BuildMessage(@event),
            forceUseDefaultRoutingKey: !SendByMessageSelfRoutingKey(),
            sourceOutboxUowId: @event.As<IPlatformUowEvent>()?.SourceUowId,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Default is False. If True, send message using RoutingKey() from <see cref="IPlatformSelfRoutingKeyBusMessage" />
    /// </summary>
    /// <returns></returns>
    protected virtual bool SendByMessageSelfRoutingKey()
    {
        return false;
    }

    protected PlatformBusMessageIdentity BuildPlatformEventBusMessageIdentity(IDictionary<string, object> eventRequestContext)
    {
        return new PlatformBusMessageIdentity
        {
            UserId = eventRequestContext.UserId(),
            RequestId = eventRequestContext.RequestId(),
            UserName = eventRequestContext.UserName()
        };
    }
}
