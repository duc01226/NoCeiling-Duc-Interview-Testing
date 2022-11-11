using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.UnitOfWork;

public interface IPlatformEfCoreUnitOfWork<out TDbContext> : IUnitOfWork
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public TDbContext DbContext { get; }
}

public class PlatformEfCoreUnitOfWork<TDbContext>
    : PlatformUnitOfWork<TDbContext>, IPlatformEfCoreUnitOfWork<TDbContext> where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreUnitOfWork(TDbContext dbContext) : base(dbContext)
    {
    }

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

    public override bool IsNoTransactionUow()
    {
        return false;
    }

    protected override async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
