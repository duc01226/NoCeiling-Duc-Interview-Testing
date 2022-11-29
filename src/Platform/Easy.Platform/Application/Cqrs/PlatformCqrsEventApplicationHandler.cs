using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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

    public bool IsInstanceHandlingAfterSourceUowCompleted { get; set; }

    protected virtual bool AutoOpenUow => true;

    /// <summary>
    /// Default is false. Return true to handle event only when the current uow is completed
    /// </summary>
    protected virtual bool HandleAfterUowCompleted => false;

    protected override async Task ExecuteHandleAsync(TEvent request, CancellationToken cancellationToken)
    {
        if (HandleAfterUowCompleted &&
            !IsInstanceHandlingAfterSourceUowCompleted &&
            UnitOfWorkManager.HasCurrentActiveUow())
            UnitOfWorkManager.CurrentActiveUow().OnCompleted += (sender, args) =>
            {
                PlatformApplicationGlobal.RootServiceProvider.ExecuteInjectScopedAsync(
                    async (IServiceProvider sp) =>
                    {
                        var thisHandlerNewInstance = sp.GetServices<INotificationHandler<TEvent>>()
                            .First(p => p.GetType() == GetType())
                            .As<PlatformCqrsEventApplicationHandler<TEvent>>()
                            .With(_ => _.IsInstanceHandlingAfterSourceUowCompleted = true);

                        await thisHandlerNewInstance.Handle(request, default);
                    }).WaitResult();
            };
        else
            await InternalExecuteHandleAsync(request, cancellationToken);
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
