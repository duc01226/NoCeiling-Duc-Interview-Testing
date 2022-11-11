using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs;

public abstract class PlatformCqrsEventApplicationHandler<TEvent> : PlatformCqrsEventHandler<TEvent>
    where TEvent : PlatformCqrsEvent, new()
{
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager) : base(loggerFactory)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    protected override async Task ExecuteHandleAsync(TEvent request, CancellationToken cancellationToken)
    {
        if (AutoOpenUow())
            using (var uow = UnitOfWorkManager.Begin())
            {
                await HandleAsync(request, cancellationToken);
                await uow.CompleteAsync(cancellationToken);
            }
        else
            await HandleAsync(request, cancellationToken);
    }

    protected virtual bool AutoOpenUow()
    {
        return true;
    }
}
