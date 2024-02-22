using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.UnitOfWork;

/// <summary>
/// Unit of work manager.
/// Used to begin and control a unit of work.
/// </summary>
public interface IUnitOfWorkManager : IDisposable
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IUnitOfWorkManager)}");

    /// <summary>
    /// A single separated global uow in current scoped is used by repository for read data using query, usually when need to return data
    /// as enumerable to help download data like streaming data (not load all big data into ram) <br />
    /// or any other purpose that just want to using query directly without think about uow of the query. <br />
    /// This uow is auto created once per scope when access it. <br />
    /// This won't affect the normal current uow queue list when Begin a new uow.
    /// </summary>
    public IUnitOfWork GlobalUow { get; }

    public IPlatformCqrs CurrentSameScopeCqrs { get; }

    /// <summary>
    /// Just create and return a new instance of uow without manage it. It will not affect to <see cref="HasCurrentActiveUow" /> result
    /// </summary>
    public IUnitOfWork CreateNewUow();

    /// <summary>
    /// Gets last unit of work (or null if not exists).
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork CurrentUow();

    /// <summary>
    /// Gets currently latest active unit of work.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public IUnitOfWork CurrentActiveUow();

    /// <summary>
    /// Gets currently latest or created active unit of work has id equal uowId.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public IUnitOfWork CurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    /// Gets currently latest active unit of work of type <see cref="TUnitOfWork" />.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    /// <remarks>
    /// The method is used to retrieve the latest active unit of work of a specific type from the current scope. A unit of work, in this context, represents a transactional set of operations that are either all committed or all rolled back.
    /// <br />
    /// This method is particularly useful when you have different types of units of work and you need to retrieve the current active one of a specific type. For instance, you might have different types of units of work for handling different domains or different types of database transactions.
    /// <br />
    /// The method will throw an exception if there is no active unit of work of the specified type. This ensures that the method always returns a valid, active unit of work of the specified type, or fails explicitly, preventing silent failures or unexpected behavior due to a missing or inactive unit of work.
    /// <br />
    /// In the PlatformUnitOfWorkManager class, this method is implemented by first retrieving the current unit of work and then checking if it is of the specified type and if it is active. If these conditions are not met, an exception is thrown.
    /// </remarks>
    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork;

    /// <summary>
    /// Gets currently latest active unit of work. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork TryGetCurrentActiveUow();

    /// <summary>
    /// Gets currently latest or created active unit of work has id equal uowId. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork TryGetCurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    /// Check that is there any currently latest active unit of work
    /// </summary>
    public bool HasCurrentActiveUow();

    /// <summary>
    /// Check that is there any currently latest or created active unit of work has id equal uowId
    /// </summary>
    public bool HasCurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    /// Start a new unit of work. <br />
    /// If current active unit of work is existing, return it. <br />
    /// When suppressCurrentUow=true, new uow will be created even if current uow is existing. When false, use
    /// current active uow if possible. <br />
    /// Default is true.
    /// </summary>
    /// <param name="suppressCurrentUow">If set to true, a new unit of work will be created even if a current unit of work exists. If set to false, the current active unit of work will be used if possible. Default value is true.</param>
    /// <returns>Returns an instance of the unit of work.</returns>
    /// <remarks>
    /// The Begin method in the IUnitOfWorkManager interface is used to start a new unit of work in the context of the application. A unit of work, in this context, represents a transactional boundary for operations that need to be executed together.
    /// <br />
    /// The Begin method takes a boolean parameter suppressCurrentUow which, when set to true, forces the creation of a new unit of work even if there is an existing active unit of work. If set to false, the method will use the current active unit of work if one exists. By default, this parameter is set to true.
    /// <br />
    /// This method is used in various parts of the application where a set of operations need to be executed within a transactional boundary.
    /// <br />
    /// In these classes, the Begin method is used to start a unit of work, after which various operations are performed. Once all operations are completed, the unit of work is completed by calling the CompleteAsync method on the unit of work instance. This ensures that all operations within the unit of work are executed as a single transaction.
    /// </remarks>
    public IUnitOfWork Begin(bool suppressCurrentUow = true);

    /// <summary>
    /// Remove all managed inactive uow to clear memory
    /// </summary>
    public void RemoveAllInactiveUow();
}

public abstract class PlatformUnitOfWorkManager(IPlatformCqrs currentSameScopeCqrs, IPlatformRootServiceProvider rootServiceProvider)
    : IUnitOfWorkManager
{
    protected readonly List<IUnitOfWork> CurrentUnitOfWorks = [];
    protected readonly ConcurrentDictionary<string, IUnitOfWork> FreeCreatedUnitOfWorks = new();
    protected readonly SemaphoreSlim RemoveAllInactiveUowLock = new(1, 1);
    protected readonly IPlatformRootServiceProvider RootServiceProvider = rootServiceProvider;
    private bool disposed;

    private IUnitOfWork globalUow;
    private bool isDisposing;

    public IPlatformCqrs CurrentSameScopeCqrs { get; } = currentSameScopeCqrs;

    public abstract IUnitOfWork CreateNewUow();

    public virtual IUnitOfWork CurrentUow()
    {
        RemoveAllInactiveUow();

        return CurrentUnitOfWorks.LastOrDefault();
    }

    public IUnitOfWork CurrentActiveUow()
    {
        var currentUow = CurrentUow();

        EnsureUowActive(currentUow);

        return currentUow;
    }

    public IUnitOfWork CurrentOrCreatedActiveUow(string uowId)
    {
        var currentUow = CurrentOrCreatedUow(uowId);

        EnsureUowActive(currentUow);

        return currentUow;
    }

    public IUnitOfWork TryGetCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true
            ? CurrentUow()
            : null;
    }

    public IUnitOfWork TryGetCurrentOrCreatedActiveUow(string uowId)
    {
        if (uowId == null) return TryGetCurrentActiveUow();

        var currentOrCreatedUow = CurrentOrCreatedUow(uowId);

        return currentOrCreatedUow?.IsActive() == true
            ? currentOrCreatedUow
            : null;
    }

    public bool HasCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true;
    }

    public bool HasCurrentOrCreatedActiveUow(string uowId)
    {
        return CurrentOrCreatedUow(uowId)?.IsActive() == true;
    }

    public virtual IUnitOfWork Begin(bool suppressCurrentUow = true)
    {
        RemoveAllInactiveUow();

        if (suppressCurrentUow || CurrentUnitOfWorks.IsEmpty()) CurrentUnitOfWorks.Add(CreateNewUow());

        return CurrentUow();
    }

    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        var uowOfType = CurrentUow()?.UowOfType<TUnitOfWork>();

        return uowOfType
            .Ensure(
                must: currentUow => currentUow != null,
                $"There's no current any uow of type {typeof(TUnitOfWork).FullName} has been begun.")
            .Ensure(
                must: currentUow => currentUow.IsActive(),
                $"Current unit of work of type {typeof(TUnitOfWork).FullName} has been completed or disposed.");
    }

    public IUnitOfWork GlobalUow => globalUow ??= CreateNewUow();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void RemoveAllInactiveUow()
    {
        if (disposed || isDisposing) return;

        List<IUnitOfWork> removedUOWs = [];
        try
        {
            RemoveAllInactiveUowLock.Wait();

            CurrentUnitOfWorks.RemoveWhere(p => !p.IsActive(), out removedUOWs);

            FreeCreatedUnitOfWorks.Keys
                .Where(key => !FreeCreatedUnitOfWorks[key].IsActive())
                .ForEach(
                    inactivatedUowKey =>
                    {
                        FreeCreatedUnitOfWorks.TryRemove(inactivatedUowKey, out var removedUOW);
                        if (removedUOW != null) removedUOWs.Add(removedUOW);
                    });
        }
        finally
        {
            RemoveAllInactiveUowLock.Release();

            // Must dispose removedUOWs after release lock to prevent forever lock because
            // Dispose => trigger RemoveAllInactiveUow again => in same thread the lock hasn't been released yet
            removedUOWs.ForEach(p => p.Dispose());
        }
    }

    public virtual IUnitOfWork CurrentOrCreatedUow(string uowId)
    {
        RemoveAllInactiveUow();

        return LastOrDefaultMatchedUowOfId(CurrentUnitOfWorks, uowId) ?? LastOrDefaultMatchedUowOfId(FreeCreatedUnitOfWorks.Values.ToList(), uowId);

        static IUnitOfWork LastOrDefaultMatchedUowOfId(List<IUnitOfWork> unitOfWorks, string uowId)
        {
            for (var i = unitOfWorks.Count - 1; i >= 0; i--)
            {
                var matchedUow = unitOfWorks.ElementAtOrDefault(i)?.UowOfId(uowId);

                if (matchedUow != null) return matchedUow;
            }

            return null;
        }
    }

    private static void EnsureUowActive(IUnitOfWork currentUow)
    {
        currentUow
            .Ensure(
                must: currentUow => currentUow != null,
                "There's no current any uow has been begun.")
            .Ensure(
                must: currentUow => currentUow.IsActive(),
                "Current unit of work has been completed or disposed.");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            isDisposing = true;

            if (disposing)
            {
                // Release managed resources
                // ToList to clone the list to dispose because dispose could cause trigger RemoveAllInactiveUow => modified the original list
                CurrentUnitOfWorks.ToList().ForEach(currentUnitOfWork => currentUnitOfWork?.Dispose());
                CurrentUnitOfWorks.Clear();

                // Release managed resources
                // ToList to clone the list to dispose because dispose could cause trigger RemoveAllInactiveUow => modified the original list
                FreeCreatedUnitOfWorks.ToList().ForEach(currentUnitOfWork => currentUnitOfWork.Value?.Dispose());
                FreeCreatedUnitOfWorks.Clear();

                globalUow?.Dispose();
                globalUow = null;

                RemoveAllInactiveUowLock.Dispose();
            }

            // Release unmanaged resources

            disposed = true;
            isDisposing = false;
        }
    }

    ~PlatformUnitOfWorkManager()
    {
        Dispose(false);
    }
}

public static class UnitOfWorkManagerExtension
{
    public static async Task ExecuteInNewUow(this IUnitOfWorkManager unitOfWorkManager, Func<IUnitOfWork, Task> actionFn, bool suppressCurrentUow = true)
    {
        using (var uow = unitOfWorkManager.Begin(suppressCurrentUow))
        {
            await actionFn(uow);

            await uow.CompleteAsync();
        }
    }

    public static async Task<TResult> ExecuteInNewUow<TResult>(
        this IUnitOfWorkManager unitOfWorkManager,
        Func<IUnitOfWork, Task<TResult>> actionFn,
        bool suppressCurrentUow = true)
    {
        using (var uow = unitOfWorkManager.Begin(suppressCurrentUow))
        {
            var result = await actionFn(uow);

            await uow.CompleteAsync();

            return result;
        }
    }
}
