using Easy.Platform.Application.Persistence;
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
    public PlatformPersistenceUnitOfWork(TDbContext dbContext)
    {
        DbContext = dbContext.With(_ => _.MappedUnitOfWork = this);
    }

    public TDbContext DbContext { get; }

    // Protected implementation of Dispose pattern.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            // Dispose managed state (managed objects).
            DbContext?.Dispose();

        Disposed = true;
    }
}
