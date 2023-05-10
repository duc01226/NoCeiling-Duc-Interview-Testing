using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
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
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(loggerFactory, unitOfWorkManager)
    {
        ApplicationBusMessageProducer = applicationBusMessageProducer;
        UserContextAccessor = userContextAccessor;
        ApplicationSettingContext = applicationSettingContext;
    }

    protected override bool EnableHandleEventFromInboxBusMessage => false;

    protected IPlatformApplicationUserContextAccessor UserContextAccessor { get; }

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    protected abstract TMessage BuildMessage(TEvent @event);

    protected override async Task HandleAsync(
        TEvent @event,
        CancellationToken cancellationToken)
    {
        await SendMessage(@event, cancellationToken);
    }

    protected override void LogError(TEvent notification, Exception exception)
    {
        Logger.LogError(
            exception,
            "[PlatformCqrsEventBusMessageProducer] Failed to send {MessageName}. MessageContent: {MessageContent}",
            nameof(TMessage),
            exception.As<PlatformMessageBusException<TMessage>>()?.EventBusMessage.ToJson());
    }

    protected virtual async Task SendMessage(TEvent @event, CancellationToken cancellationToken)
    {
        await ApplicationBusMessageProducer.SendAsync(
            BuildMessage(@event),
            forceUseDefaultRoutingKey: !SendByMessageSelfRoutingKey(),
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

    protected PlatformBusMessageIdentity BuildPlatformEventBusMessageIdentity()
    {
        return new PlatformBusMessageIdentity
        {
            UserId = UserContextAccessor.Current.UserId(),
            RequestId = UserContextAccessor.Current.RequestId(),
            UserName = UserContextAccessor.Current.UserName()
        };
    }
}
