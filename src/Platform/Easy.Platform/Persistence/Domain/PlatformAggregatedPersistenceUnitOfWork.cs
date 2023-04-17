using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Persistence.Domain;

public interface IPlatformAggregatedPersistenceUnitOfWork : IUnitOfWork
{
    public bool IsPseudoTransactionUow<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork;
}

/// <summary>
/// The aggregated unit of work is to support multi database type in a same application.
/// Each item in InnerUnitOfWorks present a REAL unit of work including a db context
/// </summary>
public class PlatformAggregatedPersistenceUnitOfWork : PlatformUnitOfWork, IPlatformAggregatedPersistenceUnitOfWork
{
    /// <summary>
    /// Store associatedServiceScope to destroy it when uow is create, using and destroy
    /// </summary>
    private IServiceScope associatedServiceScope;

    public PlatformAggregatedPersistenceUnitOfWork(List<IUnitOfWork> innerUnitOfWorks, IServiceScope associatedServiceScope)
    {
        InnerUnitOfWorks = innerUnitOfWorks ?? new List<IUnitOfWork>();
        this.associatedServiceScope = associatedServiceScope;
    }

    public override bool IsPseudoTransactionUow()
    {
        return InnerUnitOfWorks.All(p => p.IsPseudoTransactionUow());
    }

    public bool IsPseudoTransactionUow<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork
    {
        return InnerUnitOfWorks.FirstOrDefault(p => p.Equals(uow))?.IsPseudoTransactionUow() == true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            InnerUnitOfWorks.ForEach(p => p.Dispose());
            InnerUnitOfWorks.Clear();

            if (associatedServiceScope != null)
            {
                var associatedServiceScopeToDispose = associatedServiceScope;

                associatedServiceScope = null;

                associatedServiceScopeToDispose.Dispose();
            }
        }

        Disposed = true;
    }
}
