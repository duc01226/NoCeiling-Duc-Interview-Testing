using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Events;

namespace Easy.Platform.Domain.Services;

/// <summary>
/// Domain service is used to serve business logic operation related to many root domain entities,
/// the business logic term is understood by domain expert.
/// </summary>
public interface IPlatformDomainService
{
}

public abstract class PlatformDomainService : IPlatformDomainService
{
    protected readonly IPlatformCqrs Cqrs;

    public PlatformDomainService(
        IPlatformCqrs cqrs)
    {
        Cqrs = cqrs;
    }

    protected Task SendEvent<TEvent>(TEvent domainEvent, CancellationToken token = default)
        where TEvent : PlatformCqrsEvent
    {
        return Cqrs.SendEvent(domainEvent, token);
    }
}
