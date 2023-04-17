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

    protected virtual bool AutoOpenUow => true;

    protected override async Task ExecuteHandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        await InternalExecuteHandleAsync(@event, cancellationToken);
    }

    private async Task InternalExecuteHandleAsync(TEvent request, CancellationToken cancellationToken)
    {
        if (AutoOpenUow)
            using (var uow = UnitOfWorkManager.Begin())
            {
                await HandleAsync(request, cancellationToken);
                await uow.CompleteAsync(cancellationToken);
            }
        else
            await HandleAsync(request, cancellationToken);
    }
}
