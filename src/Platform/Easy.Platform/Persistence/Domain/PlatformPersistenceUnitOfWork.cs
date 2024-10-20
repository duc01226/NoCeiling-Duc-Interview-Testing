using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Persistence.Domain;

public interface IPlatformPersistenceUnitOfWork<out TDbContext> : IPlatformUnitOfWork
    where TDbContext : IPlatformDbContext
{
    public TDbContext DbContext { get; }
}

public abstract class PlatformPersistenceUnitOfWork<TDbContext> : PlatformUnitOfWork, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    protected Lazy<TDbContext> LazyDbContext;

    public PlatformPersistenceUnitOfWork(IPlatformRootServiceProvider rootServiceProvider, IServiceProvider serviceProvider) : base(rootServiceProvider)
    {
        ServiceProvider = serviceProvider;
        LazyDbContext = new Lazy<TDbContext>(
            () => DbContextFactory(serviceProvider).With(dbContext => dbContext.MappedUnitOfWork = this),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    protected IServiceProvider ServiceProvider { get; }
    public TDbContext DbContext => LazyDbContext.Value;

    protected override async Task InternalSaveChangesAsync(CancellationToken cancellationToken)
    {
        if (LazyDbContext.IsValueCreated)
            await DbContext.SaveChangesAsync(cancellationToken);
    }

    // Protected implementation of Dispose pattern.
    protected override void Dispose(bool disposing)
    {
        if (!Disposed)
        {
            base.Dispose(disposing);

            // Release managed resources
            if (disposing)
            {
                if (ShouldDisposeDbContext())
                {
                    BeforeDisposeDbContext(DbContext);
                    DbContext?.Dispose();
                }

                LazyDbContext = null;
            }

            Disposed = true;
        }
    }

    protected virtual bool ShouldDisposeDbContext()
    {
        return LazyDbContext?.IsValueCreated == true;
    }

    protected virtual void BeforeDisposeDbContext(TDbContext dbContext)
    {
    }

    protected virtual TDbContext DbContextFactory(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<TDbContext>();
    }
}
