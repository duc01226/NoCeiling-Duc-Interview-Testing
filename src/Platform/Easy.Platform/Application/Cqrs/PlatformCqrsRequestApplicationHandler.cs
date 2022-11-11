using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Timing;

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

    public IPlatformCqrsRequest PopulateAuditInfo(TRequest request)
    {
        return request.PopulateAuditInfo(
            auditTrackId: Guid.NewGuid(),
            auditRequestDate: Clock.Now,
            auditRequestByUserId: UserContext.Current.UserId());
    }
}
