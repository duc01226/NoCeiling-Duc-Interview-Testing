using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Validations;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs;

public abstract class PlatformCqrsRequestApplicationHandler<TRequest> : PlatformCqrsRequestHandler<TRequest>
    where TRequest : IPlatformCqrsRequest
{
    protected readonly IPlatformApplicationUserContextAccessor UserContext;

    public PlatformCqrsRequestApplicationHandler(IPlatformApplicationUserContextAccessor userContext)
    {
        UserContext = userContext;
        Logger = PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformCqrsRequestApplicationHandler<>));
    }

    public IPlatformApplicationUserContext CurrentUser => UserContext.Current;

    public ILogger Logger { get; }

    public IPlatformCqrsRequestAuditInfo BuildRequestAuditInfo(TRequest request)
    {
        return new PlatformCqrsRequestAuditInfo(
            auditTrackId: Guid.NewGuid(),
            auditRequestByUserId: UserContext.Current.UserId());
    }

    /// <summary>
    /// Override this function to implement additional async validation logic for the request
    /// </summary>
    protected virtual async Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(
        PlatformValidationResult<TRequest> requestSelfValidation,
        CancellationToken cancellationToken)
    {
        return requestSelfValidation;
    }

    protected virtual Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        return ValidateRequestAsync(request.Validate().Of<TRequest>(), cancellationToken);
    }
}
