using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;

public interface IPlatformCqrsDomainEventBusMessageConsumer<TDomainEvent> : IPlatformApplicationMessageBusConsumer<PlatformBusMessage<TDomainEvent>>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
}

public abstract class PlatformCqrsDomainEventBusMessageConsumer<TDomainEvent>
    : PlatformApplicationMessageBusConsumer<PlatformBusMessage<TDomainEvent>>, IPlatformCqrsDomainEventBusMessageConsumer<TDomainEvent>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventBusMessageConsumer(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerBuilder, uowManager, serviceProvider)
    {
    }
}
