using Easy.Platform.Common;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
            PlatformGlobal.LoggerFactory.CreateLogger(GetType())
                .LogWarning(
                    ex,
                    $"{GetType().Name} complete failed because of version conflict. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]");

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

    protected override async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
