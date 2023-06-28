using System.Diagnostics;
using System.Reflection.Metadata;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Exceptions.Extensions;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.Caching;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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
    protected readonly IPlatformCacheRepositoryProvider CacheRepositoryProvider;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsQueryApplicationHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager) : base(userContext)
    {
        UnitOfWorkManager = unitOfWorkManager;
        CacheRepositoryProvider = PlatformGlobal.RootServiceProvider.GetRequiredService<IPlatformCacheRepositoryProvider>();
    }

    public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        using (var activity = IPlatformCqrsQueryApplicationHandler.ActivitySource.StartActivity($"{nameof(IPlatformCqrsQueryApplicationHandler)}.{nameof(Handle)}"))
        {
            activity?.SetTag("RequestType", request.GetType().Name);
            activity?.SetTag("Request", request.ToJson());

            request.SetAuditInfo<TQuery>(BuildRequestAuditInfo(request));

            await ValidateRequestAsync(request.Validate().Of<TQuery>(), cancellationToken).EnsureValidAsync();

            return await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                () => HandleAsync(request, cancellationToken),
                onException: ex =>
                {
                    PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformCqrsQueryApplicationHandler<,>))
                        .Log(
                            ex.IsPlatformLogicException() ? LogLevel.Warning : LogLevel.Error,
                            ex,
                            "[{Tag1}] Query:{RequestName} has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                            ex.IsPlatformLogicException() ? "LogicErrorWarning" : "UnknownError",
                            request.GetType().Name,
                            request.AuditInfo.AuditTrackId,
                            request.ToJson(),
                            CurrentUser.GetAllKeyValues().ToJson());
                });
        }
    }

    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}
