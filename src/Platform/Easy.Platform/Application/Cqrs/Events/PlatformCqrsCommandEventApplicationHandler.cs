using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsCommandEventApplicationHandler<TCommand> : PlatformCqrsEventApplicationHandler<PlatformCqrsCommandEvent<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
    protected PlatformCqrsCommandEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }

    protected override bool HandleWhen(PlatformCqrsCommandEvent<TCommand> @event)
    {
        return @event.Action == PlatformCqrsCommandEventAction.Executed;
    }
}
