using Easy.Platform.Common.Dtos;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Queries;

public interface IPlatformCqrsQuery : IPlatformCqrsRequest
{
}

public interface IPlatformCqrsQuery<out TResult> : IPlatformCqrsQuery, IRequest<TResult>
{
}

public abstract class PlatformCqrsQuery<TResult> : PlatformCqrsRequest, IPlatformCqrsQuery<TResult>
{
}

public abstract class PlatformCqrsPagedQuery<TResult, TItem> : PlatformCqrsQuery<TResult>, IPlatformPagedRequest
    where TResult : PlatformCqrsQueryPagedResult<TItem>
{
    public virtual int? SkipCount { get; set; }
    public virtual int? MaxResultCount { get; set; }

    public bool IsPagedRequestValid()
    {
        return (SkipCount == null || SkipCount >= 0) && (MaxResultCount == null || MaxResultCount >= 0);
    }
}
