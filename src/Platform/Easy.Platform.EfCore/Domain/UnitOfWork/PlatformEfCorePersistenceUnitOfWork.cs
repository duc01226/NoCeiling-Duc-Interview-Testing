using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Common;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.EfCore.Domain.UnitOfWork;

public interface IPlatformEfCorePersistenceUnitOfWork<out TDbContext> : IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
}

public class PlatformEfCorePersistenceUnitOfWork<TDbContext>
    : PlatformPersistenceUnitOfWork<TDbContext>, IPlatformEfCorePersistenceUnitOfWork<TDbContext> where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    private readonly PooledDbContextFactory<TDbContext> pooledDbContextFactory;

    public PlatformEfCorePersistenceUnitOfWork(
        IPlatformRootServiceProvider rootServiceProvider,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        DbContextOptions<TDbContext> dbContextOptions) : base(rootServiceProvider, serviceProvider, loggerFactory)
    {
        PersistenceConfiguration = persistenceConfiguration;
        DbContextOptions = dbContextOptions;
        pooledDbContextFactory = serviceProvider.GetService<PooledDbContextFactory<TDbContext>>();
    }

    protected override SemaphoreSlim NotThreadSafeDbContextLock { get; } = new(ContextMaxConcurrentThreadLock, ContextMaxConcurrentThreadLock);

    protected IPlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration { get; }

    protected DbContextOptions<TDbContext> DbContextOptions { get; }

    public override bool IsPseudoTransactionUow()
    {
        return false;
    }

    public override bool MustKeepUowForQuery()
    {
        return DbContextOptions.IsUsingLazyLoadingProxy();
    }

    public override TEntity SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(
        TEntity existingEntity,
        bool useExactGenericTypeNotRuntimeType = false,
        Func<TEntity, Type>? customGetRuntimeTypeFn = null)
    {
        return base.SetCachedExistingOriginalEntity<TEntity, TPrimaryKey>(
            existingEntity,
            useExactGenericTypeNotRuntimeType,
            customGetRuntimeTypeFn ?? (p => DbContext.GetCachedExistingOriginalEntityCustomGetRuntimeTypeFn(p)));
    }

    protected override TDbContext DbContextFactory(IServiceProvider serviceProvider)
    {
        if (CanUsePooledDbContext())
            return pooledDbContextFactory.CreateDbContext();

        return base.DbContextFactory(serviceProvider);
    }

    private bool CanUsePooledDbContext()
    {
        return pooledDbContextFactory != null;
    }

    protected override void BeforeDisposeDbContext(TDbContext dbContext)
    {
        if (CanUsePooledDbContext())
            dbContext.As<DbContext>().ChangeTracker.Clear();
    }
}
