using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    public bool Completed { get; }

    public bool Disposed { get; }

    public List<IUnitOfWork> InnerUnitOfWorks { get; }
    public event EventHandler OnCompleted;
    public event EventHandler<UnitOfWorkFailedArgs> OnFailed;

    public TInnerUow FindFirstInnerUowOfType<TInnerUow>() where TInnerUow : class, IUnitOfWork;

    /// <summary>
    /// Completes this unit of work.
    /// It saves all changes and commit transaction if exists.
    /// Each unit of work can only Complete once
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Return true if the current uow is not Completed and not Disposed
    /// </summary>
    /// <returns></returns>
    public bool IsActive();

    /// <summary>
    /// If true, the uow actually do not handle real transaction. Repository when create/update data actually save immediately
    /// </summary>
    public bool IsNoTransactionUow();

    public TUnitOfWork CurrentInner<TUnitOfWork>() where TUnitOfWork : IUnitOfWork
    {
        return (TUnitOfWork)InnerUnitOfWorks.LastOrDefault(p => p.GetType().IsAssignableTo(typeof(TUnitOfWork)));
    }
}

public class UnitOfWorkFailedArgs
{
    public UnitOfWorkFailedArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; set; }
}

public abstract class PlatformUnitOfWork : IUnitOfWork
{
    public event EventHandler OnCompleted;
    public event EventHandler<UnitOfWorkFailedArgs> OnFailed;

    public bool Completed { get; protected set; }
    public bool Disposed { get; protected set; }
    public List<IUnitOfWork> InnerUnitOfWorks { get; protected set; } = new();

    public TInnerUow FindFirstInnerUowOfType<TInnerUow>() where TInnerUow : class, IUnitOfWork
    {
        return InnerUnitOfWorks.FindFirstInnerUowOfType<TInnerUow>();
    }

    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
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
        catch (Exception e)
        {
            InvokeOnFailed(this, new UnitOfWorkFailedArgs(e));
            throw;
        }
    }

    public bool IsActive()
    {
        return !Completed && !Disposed;
    }

    public abstract bool IsNoTransactionUow();

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    // Protected implementation of Dispose pattern.
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed state (managed objects).
        }

        Disposed = true;
    }

    protected virtual Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected void InvokeOnCompleted(object sender, EventArgs e)
    {
        OnCompleted?.Invoke(sender, e);
    }

    protected void InvokeOnFailed(object sender, UnitOfWorkFailedArgs e)
    {
        OnFailed?.Invoke(sender, e);
    }
}

public abstract class PlatformUnitOfWork<TDbContext> : PlatformUnitOfWork where TDbContext : IDisposable
{
    public PlatformUnitOfWork(TDbContext dbContext)
    {
        DbContext = dbContext;
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
