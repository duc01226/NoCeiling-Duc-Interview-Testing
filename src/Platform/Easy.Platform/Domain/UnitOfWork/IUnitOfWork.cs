using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Domain.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Generated Unique Uow Id
    /// </summary>
    public string Id { get; set; }

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

    public IUnitOfWork ParentUnitOfWork { get; set; }

    public List<Func<Task>> OnCompletedActions { get; set; }
    public List<Func<UnitOfWorkFailedArgs, Task>> OnFailedActions { get; set; }

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
    /// Return True to determine that the uow should not be disposed, must be kept for data has been query from it.
    /// </summary>
    public bool MustKeepUowForQuery();

    /// <summary>
    /// Return True to determine that this uow is Thread Safe and could support multiple parallel query
    /// </summary>
    public bool DoesSupportParallelQuery();

    /// <summary>
    /// Asynchronously wait to enter the UowLock. If no-one has been granted access to the UowLock, code execution will proceed, otherwise this thread waits here until the semaphore is released
    /// </summary>
    public Task LockAsync();

    /// <summary>
    /// When the task is ready, release the UowLock. It is vital to ALWAYS release the UowLock when we are ready, or else we will end up with a UowLock that is forever locked.
    /// This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
    /// </summary>
    public void ReleaseLock();

    /// <summary>
    /// Get itself or inner uow which is TUnitOfWork.
    /// </summary>
    public TUnitOfWork UowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        return this is TUnitOfWork
            ? this.As<TUnitOfWork>()
            : InnerUnitOfWorks.FirstUowOfType<TUnitOfWork>();
    }

    /// <summary>
    /// Get itself or inner uow which is has Id equal uowId.
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork UowOfId(string uowId)
    {
        if (Id == uowId) return this;

        for (var i = InnerUnitOfWorks.Count - 1; i >= 0; i--)
        {
            if (InnerUnitOfWorks[i].Id == uowId) return InnerUnitOfWorks[i];

            var innerUow = InnerUnitOfWorks[i].UowOfId(uowId);
            if (innerUow != null) return innerUow;
        }

        return null;
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

    protected SemaphoreSlim NotThreadSafeDbContextQueryLock { get; } = new(1, 1);

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public IUnitOfWork ParentUnitOfWork { get; set; }

    public List<Func<Task>> OnCompletedActions { get; set; } = new();
    public List<Func<UnitOfWorkFailedArgs, Task>> OnFailedActions { get; set; } = new();
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

            Completed = true;

            await InvokeOnCompletedActions();
        }
        catch (Exception e)
        {
            await InvokeOnFailedActions(new UnitOfWorkFailedArgs(e));
            throw;
        }
    }

    public bool IsActive()
    {
        return !Completed && !Disposed;
    }

    public abstract bool IsPseudoTransactionUow();

    public abstract bool MustKeepUowForQuery();

    public abstract bool DoesSupportParallelQuery();

    public Task LockAsync()
    {
        return NotThreadSafeDbContextQueryLock.WaitAsync();
    }

    public void ReleaseLock()
    {
        NotThreadSafeDbContextQueryLock.Release();
    }

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
            // Dispose managed state (managed objects).
            NotThreadSafeDbContextQueryLock.Dispose();

        Disposed = true;
    }

    protected virtual Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual async Task InvokeOnCompletedActions()
    {
        if (OnCompletedActions.IsEmpty()) return;

        Util.TaskRunner.QueueActionInBackground(
            async () =>
            {
                await OnCompletedActions.ForEachAsync(p => p.Invoke());

                OnCompletedActions.Clear();
            },
            () => PlatformGlobal.LoggerFactory.CreateLogger(GetType().Name));
    }

    protected async Task InvokeOnFailedActions(UnitOfWorkFailedArgs e)
    {
        await OnFailedActions.ForEachAsync(p => p.Invoke(e));

        OnFailedActions.Clear();
    }
}
