using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Domain.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IUnitOfWork)}");

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

    /// <summary>
    /// Default is false. When it's true, the uow determine that this is a temporarily created uow for single read/write data action
    /// </summary>
    public bool IsUsingOnceTransientUow { get; set; }

    public IUnitOfWork ParentUnitOfWork { get; set; }

    /// <summary>
    /// Gets or sets a list of actions to be executed upon the completion of the UnitOfWork.
    /// Each action in the list is a function returning a Task, allowing for asynchronous operations.
    /// </summary>
    public List<Func<Task>> OnCompletedActions { get; set; }

    /// <summary>
    /// Gets or sets the list of actions to be executed when the unit of work is disposed.
    /// Each action in the list is a function returning a Task, allowing for asynchronous operations.
    /// </summary>
    public List<Func<Task>> OnDisposedActions { get; set; }

    /// <summary>
    /// Gets or sets the list of actions to be executed when the unit of work fails.
    /// Each action is a function that takes a UnitOfWorkFailedArgs object and returns a Task.
    /// </summary>
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
    /// <remarks>
    /// The IsPseudoTransactionUow method is part of the IUnitOfWork interface in the Easy.Platform.Domain.UnitOfWork namespace. This method is used to determine whether the current unit of work (UoW) is handling a real transaction or not.
    /// <br />
    /// In the context of a UoW pattern, a real transaction implies that the changes made within the UoW are not immediately saved to the database, but are instead held until the UoW is completed. If the UoW fails, the changes can be rolled back, maintaining the integrity of the data.
    /// <br />
    /// On the other hand, a pseudo-transaction UoW implies that the changes are immediately saved to the database when they are made. This means there is no rollback mechanism if the UoW fails.
    /// <br />
    /// In the provided code, different implementations of the IUnitOfWork interface override the IsPseudoTransactionUow method to specify whether they handle real transactions or pseudo-transactions. For example, the PlatformEfCorePersistenceUnitOfWork class returns false, indicating it handles real transactions, while the PlatformMongoDbPersistenceUnitOfWork class returns true, indicating it handles pseudo-transactions.
    /// <br />
    /// This method is used in various parts of the code to decide how to handle certain operations. For example, in the PlatformCqrsEventApplicationHandler class, the IsPseudoTransactionUow method is used to determine whether to execute certain actions immediately or add them to the OnCompletedActions list to be executed when the UoW is completed.
    /// </remarks>
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
    /// It saves all changes and commit transaction if exists.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get itself or inner uow which is TUnitOfWork.
    /// </summary>
    /// <remarks>
    /// The method is part of the IUnitOfWork interface in the Easy.Platform.Domain.UnitOfWork namespace. This method is used to retrieve a unit of work of a specific type from the current unit of work or its inner units of work.
    /// <br />
    /// In the context of the Unit of Work pattern, a unit of work is a single, cohesive operation that consists of multiple steps. It's used to ensure that all these steps are completed successfully as a whole, or none of them are, to maintain the integrity of the data.
    /// <br />
    /// The method is a generic method that takes a type parameter TUnitOfWork which must be a class and implement the IUnitOfWork interface. It checks if the current unit of work (this) is of the type TUnitOfWork. If it is, it returns the current unit of work cast to TUnitOfWork. If it's not, it looks for the first unit of work of the type TUnitOfWork in its inner units of work.
    /// <br />
    /// This method is useful in scenarios where you have nested units of work and you need to retrieve a specific unit of work by its type. For example, in the provided code, UowOfType[TUnitOfWork]() is used to retrieve the current active unit of work of type IUnitOfWork to perform operations like SaveChangesAsync().
    /// </remarks>
    public TUnitOfWork UowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        return this.As<TUnitOfWork>() ?? InnerUnitOfWorks.FirstOrDefaultUowOfType<TUnitOfWork>();
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
    private const int ContextMaxConcurrentThreadLock = 1;

    protected PlatformUnitOfWork(IPlatformRootServiceProvider rootServiceProvider)
    {
        LoggerFactory = rootServiceProvider.GetRequiredService<ILoggerFactory>();
    }

    public bool Completed { get; protected set; }
    public bool Disposed { get; protected set; }

    protected SemaphoreSlim NotThreadSafeDbContextQueryLock { get; } = new(ContextMaxConcurrentThreadLock, ContextMaxConcurrentThreadLock);
    protected ILoggerFactory LoggerFactory { get; }

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public bool IsUsingOnceTransientUow { get; set; }

    public IUnitOfWork ParentUnitOfWork { get; set; }

    public List<Func<Task>> OnCompletedActions { get; set; } = [];
    public List<Func<Task>> OnDisposedActions { get; set; } = [];
    public List<Func<UnitOfWorkFailedArgs, Task>> OnFailedActions { get; set; } = [];
    public List<IUnitOfWork> InnerUnitOfWorks { get; protected set; } = [];
    public IUnitOfWorkManager CreatedByUnitOfWorkManager { get; set; }

    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
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
        catch (PlatformDomainRowVersionConflictException ex)
        {
            throw new Exception(
                $"{GetType().Name} complete uow failed. [[Exception:{ex}]]. FullStackTrace:{fullStackTrace}]]",
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

    public virtual bool IsActive()
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
        if (NotThreadSafeDbContextQueryLock.CurrentCount < ContextMaxConcurrentThreadLock)
            NotThreadSafeDbContextQueryLock.Release();
    }

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (InnerUnitOfWorks.IsEmpty()) return;

        await InnerUnitOfWorks.Where(p => p.IsActive()).ParallelAsync(p => p.SaveChangesAsync(cancellationToken));
    }

    ~PlatformUnitOfWork()
    {
        Dispose(false);
    }

    // Protected implementation of Dispose pattern.
    protected virtual void Dispose(bool disposing)
    {
        if (!Disposed)
        {
            // Release managed resources
            if (disposing) NotThreadSafeDbContextQueryLock.Dispose();

            // Release unmanaged resources

            Disposed = true;

            OnDisposedActions.ForEachAsync(p => p.Invoke()).WaitResult();
            OnDisposedActions.Clear();
        }
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
            () => LoggerFactory.CreateLogger(GetType()));
    }

    protected async Task InvokeOnFailedActions(UnitOfWorkFailedArgs e)
    {
        await OnFailedActions.ForEachAsync(p => p.Invoke(e));

        OnFailedActions.Clear();
    }
}
