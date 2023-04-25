using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.UnitOfWork;

public interface IPlatformEfCorePersistenceUnitOfWork<out TDbContext> : IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
}

public class PlatformEfCorePersistenceUnitOfWork<TDbContext>
    : PlatformPersistenceUnitOfWork<TDbContext>, IPlatformEfCorePersistenceUnitOfWork<TDbContext> where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCorePersistenceUnitOfWork(
        TDbContext dbContext,
        PlatformPersistenceConfiguration<TDbContext> persistenceConfiguration,
        DbContextOptions<TDbContext> dbContextOptions) : base(dbContext)
    {
        PersistenceConfiguration = persistenceConfiguration;
        DbContextOptions = dbContextOptions;
    }

    protected IPlatformPersistenceConfiguration PersistenceConfiguration { get; }

    protected DbContextOptions<TDbContext> DbContextOptions { get; }

    public override async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Completed)
            return;

        try
        {
            await InnerUnitOfWorks.Where(p => p.IsActive()).Select(p => p.CompleteAsync(cancellationToken)).WhenAll();
            await SaveChangesAsync(cancellationToken);
            Completed = true;
            InvokeOnCompleted(this, EventArgs.Empty);
        }
        catch (DbUpdateConcurrencyException concurrencyException)
        {
            throw new PlatformDomainRowVersionConflictException(concurrencyException.Message, concurrencyException);
        }
        catch (Exception e)
        {
            InvokeOnFailed(this, new UnitOfWorkFailedArgs(e));
            throw;
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

    protected override async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
