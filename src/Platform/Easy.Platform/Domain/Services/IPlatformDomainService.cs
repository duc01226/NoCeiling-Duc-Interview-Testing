using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;

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
    protected readonly IPlatformUnitOfWorkManager UnitOfWorkManager;

    public PlatformDomainService(
        IPlatformCqrs cqrs,
        IPlatformUnitOfWorkManager unitOfWorkManager)
    {
        Cqrs = cqrs;
        UnitOfWorkManager = unitOfWorkManager;
    }

    protected Task SendEvent<TEvent>(TEvent domainEvent, CancellationToken token = default)
        where TEvent : PlatformCqrsDomainEvent
    {
        return Cqrs.SendEvent(domainEvent.With(x => x.SourceUowId = UnitOfWorkManager.TryGetCurrentActiveUow()?.Id), token);
    }
}
