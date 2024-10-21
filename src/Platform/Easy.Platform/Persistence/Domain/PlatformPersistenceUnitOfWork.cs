using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Persistence.Domain;

public interface IPlatformPersistenceUnitOfWork<out TDbContext> : IPlatformUnitOfWork
    where TDbContext : IPlatformDbContext
{
    public TDbContext DbContext { get; }
}

public abstract class PlatformPersistenceUnitOfWork<TDbContext> : PlatformUnitOfWork, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    public PlatformPersistenceUnitOfWork(
        IPlatformRootServiceProvider rootServiceProvider,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) : base(rootServiceProvider, loggerFactory)
    {
        ServiceProvider = serviceProvider;
        DbContextLazy = new Lazy<TDbContext>(
            () => DbContextFactory(serviceProvider).With(dbContext => dbContext.MappedUnitOfWork = this));
    }

    protected IServiceProvider ServiceProvider { get; }
    protected Lazy<TDbContext> DbContextLazy { get; }

    public TDbContext DbContext => DbContextLazy.Value;

    protected override async Task InternalSaveChangesAsync(CancellationToken cancellationToken)
    {
        if (DbContextLazy.IsValueCreated)
            await DbContext.SaveChangesAsync(cancellationToken);
    }

    // Protected implementation of Dispose pattern.
    protected override void Dispose(bool disposing)
    {
        if (!Disposed)
        {
            base.Dispose(disposing);

            // Release managed resources
            if (disposing && ShouldDisposeDbContext())
            {
                BeforeDisposeDbContext(DbContext);
                DbContext?.Dispose();
            }

            Disposed = true;
        }
    }

    protected virtual bool ShouldDisposeDbContext()
    {
        return DbContextLazy.IsValueCreated;
    }

    protected virtual void BeforeDisposeDbContext(TDbContext dbContext)
    {
    }

    protected virtual TDbContext DbContextFactory(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<TDbContext>();
    }
}
