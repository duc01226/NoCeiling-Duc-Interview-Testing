using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Common.Extensions;
using MediatR;

namespace Easy.Platform.Common.Cqrs;

public interface IPlatformCqrs
{
    /// <summary>
    /// A Command is an imperative instruction to do something; it only has one handler. We will throw an error for multiple registered handlers of a command.
    /// </summary>
    Task<TResult> SendCommand<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : PlatformCqrsCommand<TResult>
        where TResult : PlatformCqrsCommandResult, new();

    /// <summary>
    /// A Command is an imperative instruction to do something; it only has one handler. We will throw an error for multiple registered handlers of a command.
    /// </summary>
    Task<TResult> SendCommand<TResult>(
        PlatformCqrsCommand<TResult> command,
        CancellationToken cancellationToken = default)
        where TResult : PlatformCqrsCommandResult, new();

    /// <summary>
    /// A Command is an imperative instruction to do something; it only has one handler. We will throw an error for multiple registered handlers of a command.
    /// Send a command without any result needed
    /// </summary>
    Task SendCommand<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>;

    /// <summary>
    /// To get data by conditions defined in query object.
    /// </summary>
    Task<TResult> SendQuery<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : PlatformCqrsQuery<TResult>;

    /// <summary>
    /// To get data by conditions defined in query object.
    /// </summary>
    Task<TResult> SendQuery<TResult>(
        PlatformCqrsQuery<TResult> query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// An Event is a notification that something has happened; it has zero or more handlers.
    /// </summary>
    Task SendEvent(
        PlatformCqrsEvent cqrsEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send multiple events.
    /// </summary>
    Task SendEvents(
        IEnumerable<PlatformCqrsEvent> cqrsEvents,
        CancellationToken cancellationToken = default);
}

public class PlatformCqrs : IPlatformCqrs
{
    private readonly IMediator mediator;

    public PlatformCqrs(IMediator mediator)
    {
        this.mediator = mediator;
    }

    public Task<TResult> SendCommand<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : PlatformCqrsCommand<TResult>
        where TResult : PlatformCqrsCommandResult, new()
    {
        return mediator.Send(command, cancellationToken);
    }

    public Task<TResult> SendCommand<TResult>(
        PlatformCqrsCommand<TResult> command,
        CancellationToken cancellationToken = default) where TResult : PlatformCqrsCommandResult, new()
    {
        return mediator.Send(command, cancellationToken);
    }

    public Task SendCommand<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>
    {
        return mediator.Send(command, cancellationToken);
    }

    public Task<TResult> SendQuery<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : PlatformCqrsQuery<TResult>
    {
        return mediator.Send(query, cancellationToken);
    }

    public Task<TResult> SendQuery<TResult>(
        PlatformCqrsQuery<TResult> query,
        CancellationToken cancellationToken = default)
    {
        return mediator.Send(query, cancellationToken);
    }

    public Task SendEvent(PlatformCqrsEvent cqrsEvent, CancellationToken cancellationToken = default)
    {
        return mediator.Publish(cqrsEvent, cancellationToken);
    }

    public Task SendEvents(
        IEnumerable<PlatformCqrsEvent> cqrsEvents,
        CancellationToken cancellationToken = default)
    {
        return cqrsEvents.ParallelAsync(cqrsEvent => mediator.Publish(cqrsEvent, cancellationToken));
    }
}
