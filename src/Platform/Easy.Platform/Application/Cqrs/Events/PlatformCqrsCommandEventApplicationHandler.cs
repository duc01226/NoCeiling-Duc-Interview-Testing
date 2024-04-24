using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsCommandEventApplicationHandler<TCommand, TCommandResult>
    : PlatformCqrsEventApplicationHandler<PlatformCqrsCommandEvent<TCommand, TCommandResult>>
    where TCommand : PlatformCqrsCommand<TCommandResult>, IPlatformCqrsRequest, new()
    where TCommandResult : PlatformCqrsCommandResult, new()
{
    protected PlatformCqrsCommandEventApplicationHandler(
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

    protected override bool HandleWhen(PlatformCqrsCommandEvent<TCommand, TCommandResult> @event)
    {
        return @event.Action == PlatformCqrsCommandEventAction.Executed;
    }
}

public abstract class PlatformCqrsCommandEventApplicationHandler<TCommand> : PlatformCqrsCommandEventApplicationHandler<TCommand, PlatformCqrsCommandResult>
    where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>, IPlatformCqrsRequest, new()
{
    protected PlatformCqrsCommandEventApplicationHandler(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, unitOfWorkManager, serviceProvider, rootServiceProvider)
    {
    }
}
