using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Validations;
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

public abstract class PlatformCqrsPagedResultQuery<TResult, TItem>
    : PlatformCqrsQuery<TResult>, IPlatformPagedRequest<PlatformCqrsPagedResultQuery<TResult, TItem>>
    where TResult : PlatformCqrsQueryPagedResult<TItem>
{
    public virtual int? SkipCount { get; set; }
    public virtual int? MaxResultCount { get; set; }

    public bool IsPagedRequestValid()
    {
        return (SkipCount == null || SkipCount >= 0) && (MaxResultCount == null || MaxResultCount >= 0);
    }

    public new PlatformValidationResult<PlatformCqrsPagedResultQuery<TResult, TItem>> Validate()
    {
        return PlatformValidationResult<PlatformCqrsPagedResultQuery<TResult, TItem>>.Valid(this);
    }
}
