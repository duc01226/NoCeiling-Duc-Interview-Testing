using System.Diagnostics;
using System.Reflection.Metadata;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Exceptions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Common.Exceptions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Exceptions.Extensions;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Queries;

public interface IPlatformCqrsQueryApplicationHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(Handle)}{nameof(IPlatformCqrsQuery)}");
}

public abstract class PlatformCqrsQueryApplicationHandler<TQuery, TResult>
    : PlatformCqrsRequestApplicationHandler<TQuery>, IPlatformCqrsQueryApplicationHandler, IRequestHandler<TQuery, TResult>
    where TQuery : PlatformCqrsQuery<TResult>, IPlatformCqrsRequest
{
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsQueryApplicationHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager) : base(userContext)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        using (var activity = IPlatformCqrsQueryApplicationHandler.ActivitySource.StartActivity($"{nameof(IPlatformCqrsQueryApplicationHandler)}.{nameof(Handle)}"))
        {
            activity?.SetTag("RequestType", request.GetType().Name);
            activity?.SetTag("Request", request.AsJson());

            var validRequest = (TQuery)PopulateAuditInfo(request)
                .Validate()
                .EnsureValidationValid();

            return await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                () => HandleAsync(validRequest, cancellationToken),
                onException: ex =>
                {
                    if (ex is PlatformPermissionException ||
                        ex is PlatformNotFoundException ||
                        ex is PlatformApplicationException ||
                        ex is PlatformDomainException)
                        PlatformApplicationGlobal.LoggerFactory.CreateLogger(GetType())
                            .LogWarning(
                                ex,
                                "[{Tag1}] Query has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                                "LogicErrorWarning",
                                request.AuditTrackId,
                                request.AsJson(),
                                CurrentUser.GetAllKeyValues().AsJson());
                });
        }
    }

    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}
