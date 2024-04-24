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
    private Lazy<TDbContext> lazyDbContext;

    public PlatformPersistenceUnitOfWork(IPlatformRootServiceProvider rootServiceProvider, IServiceProvider serviceProvider) : base(rootServiceProvider)
    {
        ServiceProvider = serviceProvider;
        lazyDbContext = new Lazy<TDbContext>(
            () => DbContextFactory(serviceProvider).With(dbContext => dbContext.MappedUnitOfWork = this),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    protected IServiceProvider ServiceProvider { get; }
    public TDbContext DbContext => lazyDbContext.Value;

    protected override async Task InternalSaveChangesAsync(CancellationToken cancellationToken)
    {
        if (lazyDbContext.IsValueCreated)
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
                if (lazyDbContext.IsValueCreated)
                {
                    BeforeDisposeDbContext(DbContext);
                    DbContext?.Dispose();
                }

                lazyDbContext = null;
            }

            Disposed = true;
        }
    }

    protected virtual void BeforeDisposeDbContext(TDbContext dbContext)
    {
    }

    protected virtual TDbContext DbContextFactory(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<TDbContext>();
    }
}
