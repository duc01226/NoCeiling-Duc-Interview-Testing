using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Common.Validations.Exceptions.Extensions;
using MediatR;

namespace Easy.Platform.Common.Cqrs.Commands;

public abstract class PlatformCqrsCommandHandler<TCommand, TResult>
    : PlatformCqrsRequestHandler<TCommand>, IRequestHandler<TCommand, TResult>
    where TCommand : PlatformCqrsCommand<TResult>, IPlatformCqrsRequest, new()
    where TResult : PlatformCqrsCommandResult, new()
{
    protected readonly IPlatformCqrs Cqrs;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;

    public PlatformCqrsCommandHandler(IPlatformCqrs cqrs, IPlatformRootServiceProvider rootServiceProvider)
    {
        Cqrs = cqrs;
        RootServiceProvider = rootServiceProvider;
    }

    public virtual async Task<TResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        request.Validate().WithValidationException().EnsureValid();

        var result = await ExecuteHandleAsync(request, cancellationToken);

        if (RootServiceProvider.CheckAssignableToServiceRegistered(typeof(IPlatformCqrsEventApplicationHandler<PlatformCqrsCommandEvent<TCommand, TResult>>)))
            await Cqrs.SendEvent(
                new PlatformCqrsCommandEvent<TCommand, TResult>(request, result, PlatformCqrsCommandEventAction.Executed),
                cancellationToken);

        return result;
    }

    protected abstract Task<TResult> HandleAsync(TCommand request, CancellationToken cancellationToken);

    protected virtual async Task<TResult> ExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        var result = await HandleAsync(request, cancellationToken);

        return result;
    }
}

public abstract class PlatformCqrsCommandHandler<TCommand> : PlatformCqrsCommandHandler<TCommand, PlatformCqrsCommandResult>
    where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>, IPlatformCqrsRequest, new()
{
    public PlatformCqrsCommandHandler(
        IPlatformCqrs cqrs,
        IPlatformRootServiceProvider rootServiceProvider) : base(cqrs, rootServiceProvider)
    {
    }

    public abstract Task HandleNoResult(TCommand request, CancellationToken cancellationToken);

    protected override async Task<PlatformCqrsCommandResult> HandleAsync(
        TCommand request,
        CancellationToken cancellationToken)
    {
        await HandleNoResult(request, cancellationToken);
        return new PlatformCqrsCommandResult();
    }
}
