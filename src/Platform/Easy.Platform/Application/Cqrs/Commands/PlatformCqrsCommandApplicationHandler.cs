using System.Diagnostics;
using System.Reflection.Metadata;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Exceptions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Common.Exceptions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Exceptions.Extensions;
using Easy.Platform.Domain.Exceptions;
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

    public virtual async Task<TResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        using (var activity = IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity($"{nameof(IPlatformCqrsCommandApplicationHandler)}.{nameof(Handle)}"))
        {
            activity?.SetTag("RequestType", request.GetType().Name);
            activity?.SetTag("Request", request.AsJson());

            var validRequest = (TCommand)PopulateAuditInfo(request)
                .Validate()
                .EnsureValidationValid();

            var result = await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                () => ExecuteHandleAsync(validRequest, cancellationToken),
                onException: ex =>
                {
                    if (ex is PlatformPermissionException ||
                        ex is PlatformNotFoundException ||
                        ex is PlatformApplicationException ||
                        ex is PlatformDomainException)
                        PlatformApplicationGlobal.LoggerFactory.CreateLogger(GetType())
                            .LogWarning(
                                ex,
                                "[{Tag1}] Command has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                                "LogicErrorWarning",
                                request.AuditTrackId,
                                request.AsJson(),
                                CurrentUser.GetAllKeyValues().AsJson());
                });

            await Cqrs.SendEvent(
                new PlatformCqrsCommandEvent<TCommand>(validRequest, PlatformCqrsCommandEventAction.Executed),
                cancellationToken);

            return result;
        }
    }

    protected abstract Task<TResult> HandleAsync(TCommand request, CancellationToken cancellationToken);

    protected virtual async Task<TResult> ExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        return await ExecuteHandleAsync(UnitOfWorkManager.Begin(), request, cancellationToken);
    }

    protected virtual async Task<TResult> ExecuteHandleAsync(
        IUnitOfWork usingUow,
        TCommand request,
        CancellationToken cancellationToken)
    {
        TResult result;
        using (usingUow)
        {
            result = await HandleAsync(request, cancellationToken);
            await Cqrs.SendEvent(
                new PlatformCqrsCommandEvent<TCommand>(request, PlatformCqrsCommandEventAction.Executing),
                cancellationToken);
            await usingUow.CompleteAsync(cancellationToken);
        }

        return result;
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
