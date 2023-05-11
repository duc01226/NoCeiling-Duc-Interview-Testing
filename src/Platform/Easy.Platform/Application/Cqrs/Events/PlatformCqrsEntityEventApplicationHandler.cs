using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsEntityEventApplicationHandler<TEntity> : PlatformCqrsEventApplicationHandler<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
    protected PlatformCqrsEntityEventApplicationHandler(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager unitOfWorkManager) : base(
        loggerBuilder,
        unitOfWorkManager)
    {
    }

    protected override bool HandleWhen(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return @event.CrudAction switch
        {
            PlatformCqrsEntityEventCrudAction.Created => true,
            PlatformCqrsEntityEventCrudAction.Updated => true,
            PlatformCqrsEntityEventCrudAction.Deleted => true,
            _ => false
        };
    }

    protected override bool CanExecuteHandlingEventUsingInboxConsumer(bool hasInboxMessageRepository, PlatformCqrsEntityEvent<TEntity> @event)
    {
        return base.CanExecuteHandlingEventUsingInboxConsumer(hasInboxMessageRepository, @event) && !@event.CrudAction.IsTrackingCrudAction();
    }
}
