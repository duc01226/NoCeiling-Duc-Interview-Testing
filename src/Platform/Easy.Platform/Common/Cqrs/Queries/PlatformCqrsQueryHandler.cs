using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Exceptions.Extensions;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Queries;

public abstract class PlatformCqrsQueryHandler<TQuery, TResult>
    : PlatformCqrsRequestHandler<TQuery>, IRequestHandler<TQuery, TResult>
    where TQuery : PlatformCqrsQuery<TResult>, IPlatformCqrsRequest
{
    protected readonly IPlatformRootServiceProvider RootServiceProvider;

    protected PlatformCqrsQueryHandler(IPlatformRootServiceProvider rootServiceProvider)
    {
        RootServiceProvider = rootServiceProvider;
    }

    public virtual async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        try
        {
            request.Validate().WithValidationException().EnsureValid();

            var result = await HandleAsync(request, cancellationToken);

            return result;
        }
        finally
        {
            Util.GarbageCollector.Collect(aggressiveImmediately: false);
        }
    }

    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}
