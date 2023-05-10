using Easy.Platform.Common.Validations.Exceptions.Extensions;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Queries;

public abstract class PlatformCqrsQueryHandler<TQuery, TResult>
    : PlatformCqrsRequestHandler<TQuery>, IRequestHandler<TQuery, TResult>
    where TQuery : PlatformCqrsQuery<TResult>, IPlatformCqrsRequest
{
    public virtual async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        request.Validate().WithValidationException().EnsureValid();

        var result = await HandleAsync(request, cancellationToken);

        return result;
    }

    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}
