using Easy.Platform.Common;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsDomainEventApplicationHandler<TEvent> : PlatformCqrsEventApplicationHandler<TEvent>
    where TEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }

    /// <summary>
    /// Default return True
    /// </summary>
    public override bool HandleWhen(TEvent @event)
    {
        return true;
    }
}
