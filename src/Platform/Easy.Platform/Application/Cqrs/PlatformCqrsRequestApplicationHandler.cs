using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Cqrs;

namespace Easy.Platform.Application.Cqrs;

public abstract class PlatformCqrsRequestApplicationHandler<TRequest> : PlatformCqrsRequestHandler<TRequest>
    where TRequest : IPlatformCqrsRequest
{
    protected readonly IPlatformApplicationUserContextAccessor UserContext;

    public PlatformCqrsRequestApplicationHandler(IPlatformApplicationUserContextAccessor userContext)
    {
        UserContext = userContext;
    }

    public IPlatformApplicationUserContext CurrentUser => UserContext.Current;

    public IPlatformCqrsRequestAuditInfo BuildRequestAuditInfo(TRequest request)
    {
        return new PlatformCqrsRequestAuditInfo(
            auditTrackId: Guid.NewGuid(),
            auditRequestByUserId: UserContext.Current.UserId());
    }
}
