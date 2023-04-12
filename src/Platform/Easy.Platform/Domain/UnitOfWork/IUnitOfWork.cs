using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// By default a uow usually present a db context, then the InnerUnitOfWorks is empty. <br />
    /// Some application could use multiple db in one service, which then the current uow could be a aggregation uow of multiple db context uow. <br />
    /// Then the InnerUnitOfWorks will hold list of all uow present all db context
    /// </summary>
    public List<IUnitOfWork> InnerUnitOfWorks { get; }

    /// <summary>
    /// Indicate it's created by UnitOfWorkManager
    /// </summary>
    public IUnitOfWorkManager CreatedByUnitOfWorkManager { get; set; }

    public event EventHandler OnCompleted;
    public event EventHandler<UnitOfWorkFailedArgs> OnFailed;

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
    public bool IsPseudoTransactionUow();

    /// <summary>
    /// Get itself or inner uow which is TUnitOfWork.
    /// </summary>
    public TUnitOfWork UowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        return this is TUnitOfWork
            ? this.As<TUnitOfWork>()
            : InnerUnitOfWorks.FirstUowOfType<TUnitOfWork>();
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
    public bool Completed { get; protected set; }
    public bool Disposed { get; protected set; }
    public event EventHandler OnCompleted;
    public event EventHandler<UnitOfWorkFailedArgs> OnFailed;
    public List<IUnitOfWork> InnerUnitOfWorks { get; protected set; } = new();
    public IUnitOfWorkManager CreatedByUnitOfWorkManager { get; set; }

    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Completed)
            return;

        try
        {
            await InnerUnitOfWorks.Where(p => p.IsActive()).Select(p => p.CompleteAsync(cancellationToken)).WhenAll();

            await SaveChangesAsync(cancellationToken);

            InvokeOnCompleted(this, EventArgs.Empty);

            Completed = true;
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

    public abstract bool IsPseudoTransactionUow();

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
