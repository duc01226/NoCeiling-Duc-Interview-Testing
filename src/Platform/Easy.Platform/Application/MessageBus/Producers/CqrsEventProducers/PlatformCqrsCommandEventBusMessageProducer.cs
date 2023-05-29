using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsCommandEventBusMessageProducer<TCommand>
    : PlatformCqrsEventBusMessageProducer<PlatformCqrsCommandEvent<TCommand>, PlatformCqrsCommandEventBusMessage<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
    protected PlatformCqrsCommandEventBusMessageProducer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    protected override PlatformCqrsCommandEventBusMessage<TCommand> BuildMessage(PlatformCqrsCommandEvent<TCommand> @event)
    {
        return PlatformCqrsCommandEventBusMessage<TCommand>.New<PlatformCqrsCommandEventBusMessage<TCommand>>(
            trackId: Guid.NewGuid().ToString(),
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsCommandEvent.EventTypeValue,
            messageAction: @event.EventAction);
    }
}

public class PlatformCqrsCommandEventBusMessage<TCommand> : PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
}
