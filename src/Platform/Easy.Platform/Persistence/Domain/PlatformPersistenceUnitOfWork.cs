using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.Persistence.Domain;

public interface IPlatformPersistenceUnitOfWork<out TDbContext> : IUnitOfWork
    where TDbContext : IPlatformDbContext
{
    public TDbContext DbContext { get; }
}

public abstract class PlatformPersistenceUnitOfWork<TDbContext> : PlatformUnitOfWork, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    public PlatformPersistenceUnitOfWork(IPlatformRootServiceProvider rootServiceProvider, TDbContext dbContext) : base(rootServiceProvider)
    {
        DbContext = dbContext.With(_ => _.MappedUnitOfWork = this);
    }

    public TDbContext DbContext { get; }

    // Protected implementation of Dispose pattern.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Dispose managed state (managed objects).
        if (disposing)
            DbContext?.Dispose();

        Disposed = true;
    }
}
