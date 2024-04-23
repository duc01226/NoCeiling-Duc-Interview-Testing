using Easy.Platform.Common;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.EfCore.Domain.UnitOfWork;

public interface IPlatformEfCorePersistenceUnitOfWork<out TDbContext> : IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
}

public class PlatformEfCorePersistenceUnitOfWork<TDbContext>
    : PlatformPersistenceUnitOfWork<TDbContext>, IPlatformEfCorePersistenceUnitOfWork<TDbContext> where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    private readonly Lazy<PooledDbContextFactory<TDbContext>> lazyPooledDbContextFactoryLazy;

    public PlatformEfCorePersistenceUnitOfWork(
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        DbContextOptions<TDbContext> dbContextOptions) : base(rootServiceProvider, serviceProvider)
    {
        PersistenceConfiguration = persistenceConfiguration;
        DbContextOptions = dbContextOptions;
        lazyPooledDbContextFactoryLazy = new Lazy<PooledDbContextFactory<TDbContext>>(
            serviceProvider.GetService<PooledDbContextFactory<TDbContext>>,
            true);
    }

    protected IPlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration { get; }

    protected DbContextOptions<TDbContext> DbContextOptions { get; }

    public override async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Completed) return;

        // Store stack trace before save changes so that if something went wrong in save into db, stack trace could
        // be tracked. Because call to db if failed lose stack trace
        var fullStackTrace = Environment.StackTrace;

        try
        {
            await InnerUnitOfWorks.Where(p => p.IsActive()).ParallelAsync(p => p.CompleteAsync(cancellationToken));

            await SaveChangesAsync(cancellationToken);

            Completed = true;

            await InvokeOnCompletedActions();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new PlatformDomainRowVersionConflictException(
                $"{GetType().Name} complete uow failed because of {nameof(DbUpdateConcurrencyException)}. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                ex);
        }
        catch (Exception ex)
        {
            await InvokeOnFailedActions(new UnitOfWorkFailedArgs(ex));

            throw new Exception(
                $"{GetType().Name} complete uow failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
                ex);
        }
    }

    public override bool IsPseudoTransactionUow()
    {
        return false;
    }

    public override bool MustKeepUowForQuery()
    {
        if (PersistenceConfiguration.MustKeepUowForQuery == null)
            return DbContextOptions.IsUsingLazyLoadingProxy();

        return PersistenceConfiguration.MustKeepUowForQuery == true;
    }

    public override bool DoesSupportParallelQuery()
    {
        return false;
    }

    protected override TDbContext DbContextFactory(IServiceProvider serviceProvider)
    {
        if (CanUsePooledDbContext())
            return lazyPooledDbContextFactoryLazy.Value.CreateDbContext();

        return base.DbContextFactory(serviceProvider);
    }

    private bool CanUsePooledDbContext()
    {
        return (PersistenceConfiguration.PooledOptions.UsePooledDbContextForUsingOnceTransientUowOnly == false || IsUsingOnceTransientUow) &&
               lazyPooledDbContextFactoryLazy.Value != null;
    }

    protected override void BeforeDisposeDbContext(TDbContext dbContext)
    {
        if (CanUsePooledDbContext())
            dbContext.As<DbContext>().ChangeTracker.Clear();
    }
}
