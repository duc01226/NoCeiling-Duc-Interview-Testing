using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Validations;
using Easy.Platform.Constants;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs;

public abstract class PlatformCqrsRequestApplicationHandler<TRequest> : PlatformCqrsRequestHandler<TRequest>
    where TRequest : IPlatformCqrsRequest
{
    protected readonly IPlatformApplicationUserContextAccessor UserContext;

    public PlatformCqrsRequestApplicationHandler(IPlatformApplicationUserContextAccessor userContext)
    {
        UserContext = userContext;
        Logger = PlatformApplicationGlobal.LoggerFactory.CreateLogger($"{DefaultPlatformLogSuffix.SystemPlatformSuffix}.{GetType().Name}");
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

    protected virtual async Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        return await ValidateRequestAsync(request.Validate().Of<TRequest>(), cancellationToken);
    }
}
