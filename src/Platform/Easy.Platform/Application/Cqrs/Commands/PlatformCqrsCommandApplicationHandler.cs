using System.Diagnostics;
using System.Reflection.Metadata;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Exceptions.Extensions;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Commands;

public interface IPlatformCqrsCommandApplicationHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(Handle)}{nameof(IPlatformCqrsCommand)}");
}

public abstract class PlatformCqrsCommandApplicationHandler<TCommand, TResult> : PlatformCqrsRequestApplicationHandler<TCommand>, IRequestHandler<TCommand, TResult>
    where TCommand : PlatformCqrsCommand<TResult>, IPlatformCqrsRequest, new()
    where TResult : PlatformCqrsCommandResult, new()
{
    protected readonly IPlatformCqrs Cqrs;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsCommandApplicationHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs) : base(userContext)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Cqrs = cqrs;
    }

    public virtual int FailedRetryCount => 0;

    protected virtual bool AutoOpenUow => true;

    public virtual async Task<TResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        using (var activity = IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity($"{nameof(IPlatformCqrsCommandApplicationHandler)}.{nameof(Handle)}"))
        {
            activity?.SetTag("RequestType", request.GetType().Name);
            activity?.SetTag("Request", request.ToJson());

            request.SetAuditInfo<TCommand>(BuildRequestAuditInfo(request));

            await ValidateRequestAsync(request.Validate().Of<TCommand>(), cancellationToken).EnsureValidAsync();

            var result = await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                () => ExecuteHandleAsync(request, cancellationToken),
                onException: ex =>
                {
                    PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformCqrsCommandApplicationHandler<>))
                        .Log(
                            ex.IsPlatformLogicException() ? LogLevel.Warning : LogLevel.Error,
                            ex,
                            "[{Tag1}] Command:{RequestName} has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                            ex.IsPlatformLogicException() ? "LogicErrorWarning" : "UnknownError",
                            request.GetType().Name,
                            request.AuditInfo.AuditTrackId,
                            request.ToJson(),
                            CurrentUser.GetAllKeyValues().ToJson());
                });

            await Cqrs.SendEvent(
                new PlatformCqrsCommandEvent<TCommand>(request, PlatformCqrsCommandEventAction.Executed).With(
                    p => p.SetRequestContextValues(CurrentUser.GetAllKeyValues())),
                cancellationToken);

            PlatformGlobal.MemoryCollector.CollectGarbageMemory();

            return result;
        }
    }

    protected abstract Task<TResult> HandleAsync(TCommand request, CancellationToken cancellationToken);

    protected virtual async Task<TResult> ExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        if (FailedRetryCount > 0)
            return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => DoExecuteHandleAsync(request, cancellationToken),
                retryCount: FailedRetryCount);
        return await DoExecuteHandleAsync(request, cancellationToken);
    }

    protected virtual async Task<TResult> DoExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        if (AutoOpenUow == false) return await HandleAsync(request, cancellationToken);

        using (var uow = UnitOfWorkManager.Begin())
        {
            var result = await HandleAsync(request, cancellationToken);

            await uow.CompleteAsync(cancellationToken);

            return result;
        }
    }
}

public abstract class PlatformCqrsCommandApplicationHandler<TCommand> : PlatformCqrsCommandApplicationHandler<TCommand, PlatformCqrsCommandResult>
    where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>, IPlatformCqrsRequest, new()
{
    public PlatformCqrsCommandApplicationHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs) : base(userContext, unitOfWorkManager, cqrs)
    {
    }

    public abstract Task HandleNoResult(TCommand request, CancellationToken cancellationToken);

    protected override async Task<PlatformCqrsCommandResult> HandleAsync(
        TCommand request,
        CancellationToken cancellationToken)
    {
        await HandleNoResult(request, cancellationToken);
        return new PlatformCqrsCommandResult();
    }
}
