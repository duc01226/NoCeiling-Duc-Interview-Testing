using MediatR;

namespace Easy.Platform.Common.Cqrs.Commands;

public interface IPlatformCqrsCommand : IPlatformCqrsRequest
{
}

public interface IPlatformCqrsCommand<out TResult> : IPlatformCqrsCommand, IRequest<TResult>
    where TResult : IPlatformCqrsCommandResult, new()
{
}

public abstract class PlatformCqrsCommand<TResult> : PlatformCqrsRequest, IPlatformCqrsCommand<TResult>
    where TResult : PlatformCqrsCommandResult, new()
{
}

public abstract class PlatformCqrsCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>
{
}
