using Easy.Platform.Common;
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
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider,
        applicationBusMessageProducer,
        applicationSettingContext)
    {
    }

    protected override PlatformCqrsCommandEventBusMessage<TCommand> BuildMessage(PlatformCqrsCommandEvent<TCommand> @event)
    {
        return PlatformCqrsCommandEventBusMessage<TCommand>.New<PlatformCqrsCommandEventBusMessage<TCommand>>(
            trackId: Guid.NewGuid().ToString(),
            payload: @event,
            identity: BuildPlatformEventBusMessageIdentity(@event.RequestContext),
            producerContext: ApplicationSettingContext.ApplicationName,
            messageGroup: PlatformCqrsCommandEvent.EventTypeValue,
            messageAction: @event.EventAction,
            requestContext: @event.RequestContext);
    }
}

public class PlatformCqrsCommandEventBusMessage<TCommand> : PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
}
