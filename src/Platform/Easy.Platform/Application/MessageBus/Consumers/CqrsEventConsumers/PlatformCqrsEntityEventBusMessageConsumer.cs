using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;

public interface IPlatformCqrsEntityEventBusMessageConsumer<in TMessage, TEntity> : IPlatformApplicationMessageBusConsumer<TMessage>
    where TEntity : class, IEntity, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
}

public abstract class PlatformCqrsEntityEventBusMessageConsumer<TMessage, TEntity>
    : PlatformApplicationMessageBusConsumer<TMessage>
    where TEntity : class, IEntity, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>, IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
    protected PlatformCqrsEntityEventBusMessageConsumer(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerBuilder, uowManager, serviceProvider)
    {
    }
}
