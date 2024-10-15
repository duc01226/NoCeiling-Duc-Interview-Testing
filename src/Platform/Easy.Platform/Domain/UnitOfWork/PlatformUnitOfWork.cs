using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Domain.UnitOfWork;

public interface IPlatformUnitOfWork : IDisposable
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformUnitOfWork)}");

    /// <summary>
    /// Generated Unique Uow Id
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// By default a uow usually present a db context, then the InnerUnitOfWorks is empty. <br />
    /// Some application could use multiple db in one service, which then the current uow could be a aggregation uow of multiple db context uow. <br />
    /// Then the InnerUnitOfWorks will hold list of all uow present all db context
    /// </summary>
    public List<IPlatformUnitOfWork> InnerUnitOfWorks { get; }

    /// <summary>
    /// Indicate it's created by UnitOfWorkManager
    /// </summary>
    public IPlatformUnitOfWorkManager CreatedByUnitOfWorkManager { get; set; }

    /// <summary>
    /// Default is false. When it's true, the uow determine that this is a temporarily created uow for single read/write data action
    /// </summary>
    public bool IsUsingOnceTransientUow { get; set; }

    public IPlatformUnitOfWork ParentUnitOfWork { get; set; }

    /// <summary>
    /// Gets or sets a list of actions to be executed upon the completion of the UnitOfWork save changes.
    /// Each action in the list is a function returning a Task, allowing for asynchronous operations.
    /// </summary>
    public List<Func<Task>> OnSaveChangesCompletedActions { get; set; }

    /// <summary>
    /// Gets or sets the list of actions to be executed when the unit of work is disposed.
    /// Each action in the list is a function returning a Task, allowing for asynchronous operations.
    /// </summary>
    public List<Func<Task>> OnDisposedActions { get; set; }

    /// <summary>
    /// Gets or sets the list of actions to be executed when the unit of work is completed.
    /// </summary>
    public List<Func<Task>> OnUowCompletedActions { get; set; }

    /// <summary>
    /// Gets or sets the list of actions to be executed when the unit of work save changes fails.
    /// Each action is a function that takes a PlatformUnitOfWorkFailedArgs object and returns a Task.
    /// </summary>
    public List<Func<PlatformUnitOfWorkFailedArgs, Task>> OnSaveChangesFailedActions { get; set; }

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
    /// This method is used in various parts of the code to decide how to handle certain operations. For example, in the PlatformCqrsEventApplicationHandler class, the IsPseudoTransactionUow method is used to determine whether to execute certain actions immediately or add them to the OnSaveChangesCompletedActions list to be executed when the UoW is completed.
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
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a cached existing original entity by its ID.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entityId">The ID of the entity.</param>
    /// <returns>The cached existing original entity if found; otherwise, null.</returns>
    /// <remarks>
    /// This method is used to retrieve an entity from the cache if it has been previously cached.
    /// It helps in reducing database calls by using the cached version of the entity.
    /// </remarks>
    public TEntity? GetCachedExistingOriginalEntity<TEntity>(string entityId) where TEntity : class, IEntity;

    /// <summary>
    /// Sets a cached existing original entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="existingEntity">The existing entity to cache.</param>
    /// <param name="needDeepCloneEntity">needDeepCloneEntity</param>
    /// <param name="runtimeEntityType">The runtime type of the entity, if different from the compile-time type.</param>
    /// <returns>The cached existing original entity.</returns>
    /// <remarks>
    /// This method is used to cache an entity, so it can be retrieved later without querying the database again.
    /// It helps in improving performance by reducing the number of database calls.
    /// </remarks>
    public TEntity SetCachedExistingOriginalEntity<TEntity>(TEntity existingEntity, Type runtimeEntityType = null)
        where TEntity : class, IEntity;

    public void RemoveCachedExistingOriginalEntity(string existingEntityId);

    public void ClearCachedExistingOriginalEntity();

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
    public TUnitOfWork UowOfType<TUnitOfWork>() where TUnitOfWork : class, IPlatformUnitOfWork
    {
        return this.As<TUnitOfWork>() ?? InnerUnitOfWorks.FirstOrDefaultUowOfType<TUnitOfWork>();
    }

    /// <summary>
    /// Get itself or inner uow which is has Id equal uowId.
    /// </summary>
    [return: MaybeNull]
    public IPlatformUnitOfWork UowOfId(string uowId)
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

public class PlatformUnitOfWorkFailedArgs
{
    public PlatformUnitOfWorkFailedArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; set; }
}

public abstract class PlatformUnitOfWork : IPlatformUnitOfWork
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
    protected ConcurrentDictionary<string, object> CachedExistingOriginalEntities { get; } = new();

    public string Id { get; set; } = Ulid.NewUlid().ToString();

    public bool IsUsingOnceTransientUow { get; set; }

    public IPlatformUnitOfWork? ParentUnitOfWork { get; set; }

    public List<Func<Task>> OnSaveChangesCompletedActions { get; set; } = [];
    public List<Func<Task>> OnDisposedActions { get; set; } = [];
    public List<Func<Task>> OnUowCompletedActions { get; set; } = [];
    public List<Func<PlatformUnitOfWorkFailedArgs, Task>> OnSaveChangesFailedActions { get; set; } = [];
    public List<IPlatformUnitOfWork> InnerUnitOfWorks { get; protected set; } = [];
    public IPlatformUnitOfWorkManager CreatedByUnitOfWorkManager { get; set; }

    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Completed) return;

        await InnerUnitOfWorks.Where(p => p.IsActive()).ParallelAsync(p => p.CompleteAsync(cancellationToken));

        await SaveChangesAsync(cancellationToken);

        await InvokeOnUowCompletedActions();

        Completed = true;
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

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Store stack trace before save changes so that if something went wrong in save into db, stack trace could
        // be tracked. Because call to db if failed lose stack trace
        // var fullStackTrace = PlatformEnvironment.StackTrace();

        try
        {
            await InnerUnitOfWorks.Where(p => p.IsActive()).ParallelAsync(p => p.SaveChangesAsync(cancellationToken));

            await InternalSaveChangesAsync(cancellationToken);

            await InvokeOnSaveChangesCompletedActions();

            CachedExistingOriginalEntities.Clear();
        }
        catch (Exception ex)
        {
            await InvokeOnSaveChangesFailedActions(new PlatformUnitOfWorkFailedArgs(ex));

            throw new Exception(
                $"{GetType().Name} save changes uow failed. [[Exception:{ex}]]",
                ex);
        }
    }

    public TEntity? GetCachedExistingOriginalEntity<TEntity>(string entityId) where TEntity : class, IEntity
    {
        if (entityId == null) return null;

        if (!CachedExistingOriginalEntities.TryGetValue(entityId, out var cachedExistingOriginalEntity))
            return ParentUnitOfWork?.GetCachedExistingOriginalEntity<TEntity>(entityId);

        return cachedExistingOriginalEntity.As<TEntity>();
    }

    public virtual TEntity SetCachedExistingOriginalEntity<TEntity>(TEntity existingEntity, Type runtimeEntityType = null)
        where TEntity : class, IEntity
    {
        var castedRuntimeTypeExistingEntity = (runtimeEntityType != null ? Convert.ChangeType(existingEntity, runtimeEntityType) : existingEntity)
            .PipeIf(p => p.As<IEntity>().HasTrackValueUpdatedDomainEventAttribute(), p => p.DeepClone());

        CachedExistingOriginalEntities.AddOrUpdate(
            existingEntity.GetId().ToString(),
            castedRuntimeTypeExistingEntity,
            (key, oldItem) => castedRuntimeTypeExistingEntity);

        return existingEntity;
    }

    public void RemoveCachedExistingOriginalEntity(string existingEntityId)
    {
        CachedExistingOriginalEntities.TryRemove(existingEntityId, out _);
    }

    public void ClearCachedExistingOriginalEntity()
    {
        CachedExistingOriginalEntities.Clear();
    }

    protected abstract Task InternalSaveChangesAsync(CancellationToken cancellationToken);

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
            if (disposing)
            {
                NotThreadSafeDbContextQueryLock.Dispose();
                CachedExistingOriginalEntities.Clear();
            }

            // Release unmanaged resources

            Disposed = true;

            OnDisposedActions
                .ParallelAsync(
                    p => Util.TaskRunner.CatchException(
                        p.Invoke,
                        ex => LoggerFactory.CreateLogger(GetType()).LogError(ex.BeautifyStackTrace(), "Invoke DisposedActions error.")))
                .Wait();
            OnDisposedActions.Clear();
        }
    }

    protected virtual async Task InvokeOnSaveChangesCompletedActions()
    {
        await OnSaveChangesCompletedActions.ParallelAsync(
            p => Util.TaskRunner.CatchException(
                p.Invoke,
                ex => LoggerFactory.CreateLogger(GetType()).LogError(ex.BeautifyStackTrace(), "Invoke CompletedActions error.")));

        OnSaveChangesCompletedActions.Clear();
    }

    protected virtual async Task InvokeOnUowCompletedActions()
    {
        await OnUowCompletedActions.ParallelAsync(
            p => Util.TaskRunner.CatchException(
                p.Invoke,
                ex => LoggerFactory.CreateLogger(GetType()).LogError(ex.BeautifyStackTrace(), "Invoke UowCompletedActions error.")));

        OnUowCompletedActions.Clear();
    }

    protected async Task InvokeOnSaveChangesFailedActions(PlatformUnitOfWorkFailedArgs e)
    {
        await OnSaveChangesFailedActions.ParallelAsync(
            p => Util.TaskRunner.CatchException(
                () => p.Invoke(e),
                ex => LoggerFactory.CreateLogger(GetType()).LogError(ex.BeautifyStackTrace(), "Invoke FailedActions error.")));

        OnSaveChangesFailedActions.Clear();
    }
}
