using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsDomainEventApplicationHandler<TEvent> : PlatformCqrsEventApplicationHandler<TEvent>
    where TEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventApplicationHandler(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager unitOfWorkManager) : base(
        loggerBuilder,
        unitOfWorkManager)
    {
    }
}
