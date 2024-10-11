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
public interface IPlatformUnitOfWorkManager : IDisposable
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformUnitOfWorkManager)}");

    /// <summary>
    /// A single separated global uow in current scoped is used by repository for read data using query, usually when need to return data
    /// as enumerable to help download data like streaming data (not load all big data into ram) <br />
    /// or any other purpose that just want to using query directly without think about uow of the query. <br />
    /// This uow is auto created once per scope when access it. <br />
    /// This won't affect the normal current uow queue list when Begin a new uow.
    /// </summary>
    public IPlatformUnitOfWork GlobalUow { get; }

    public IPlatformCqrs CurrentSameScopeCqrs { get; }

    /// <summary>
    /// Just create and return a new instance of uow without manage it. It will not affect to <see cref="HasCurrentActiveUow" /> result
    /// </summary>
    public IPlatformUnitOfWork CreateNewUow(bool isUsingOnceTransientUow);

    /// <summary>
    /// Gets last unit of work (or null if not exists).
    /// </summary>
    [return: MaybeNull]
    public IPlatformUnitOfWork CurrentUow();

    /// <summary>
    /// Gets currently latest active unit of work.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public IPlatformUnitOfWork CurrentActiveUow();

    /// <summary>
    /// Gets currently latest or created active unit of work has id equal uowId.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public IPlatformUnitOfWork CurrentOrCreatedActiveUow(string uowId);

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
    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IPlatformUnitOfWork;

    /// <summary>
    /// Gets currently latest active unit of work. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IPlatformUnitOfWork? TryGetCurrentActiveUow();

    /// <summary>
    /// Gets currently latest or created active unit of work has id equal uowId. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IPlatformUnitOfWork? TryGetCurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    /// Gets currently latest active unit of work has id equal uowId. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IPlatformUnitOfWork? TryGetCurrentActiveUow(string uowId);

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
    public IPlatformUnitOfWork Begin(bool suppressCurrentUow = true);

    /// <summary>
    /// Remove all managed inactive uow to clear memory
    /// </summary>
    public void RemoveAllInactiveUow();
}

public abstract class PlatformUnitOfWorkManager(Lazy<IPlatformCqrs> cqrs, IPlatformRootServiceProvider rootServiceProvider)
    : IPlatformUnitOfWorkManager
{
    protected readonly List<IPlatformUnitOfWork> CurrentUnitOfWorks = [];

    protected readonly Lazy<ConcurrentDictionary<string, IPlatformUnitOfWork>>
        FreeCreatedUnitOfWorks = new(() => new ConcurrentDictionary<string, IPlatformUnitOfWork>(), true);

    protected readonly SemaphoreSlim RemoveAllInactiveUowLock = new(1, 1);
    protected readonly IPlatformRootServiceProvider RootServiceProvider = rootServiceProvider;

    private bool disposed;
    private bool disposing;
    private IPlatformUnitOfWork globalUow;

    public IPlatformCqrs CurrentSameScopeCqrs => cqrs.Value;

    public abstract IPlatformUnitOfWork CreateNewUow(bool isUsingOnceTransientUow);

    public virtual IPlatformUnitOfWork CurrentUow()
    {
        return CurrentUnitOfWorks.LastOrDefault();
    }

    public IPlatformUnitOfWork CurrentActiveUow()
    {
        var currentUow = CurrentUow();

        EnsureUowActive(currentUow);

        return currentUow;
    }

    public IPlatformUnitOfWork CurrentOrCreatedActiveUow(string uowId)
    {
        var currentUow = CurrentOrCreatedUow(uowId);

        EnsureUowActive(currentUow);

        return currentUow;
    }

    public IPlatformUnitOfWork? TryGetCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true
            ? CurrentUow()
            : null;
    }

    public IPlatformUnitOfWork? TryGetCurrentOrCreatedActiveUow(string uowId)
    {
        if (uowId == null) return TryGetCurrentActiveUow();

        var currentOrCreatedUow = CurrentOrCreatedUow(uowId);

        return currentOrCreatedUow?.IsActive() == true
            ? currentOrCreatedUow
            : null;
    }

    public IPlatformUnitOfWork? TryGetCurrentActiveUow(string uowId)
    {
        if (uowId == null) return TryGetCurrentActiveUow();

        var currentOrCreatedUow = LastOrDefaultMatchedUowOfId(CurrentUnitOfWorks, uowId);

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

    public virtual IPlatformUnitOfWork Begin(bool suppressCurrentUow = true)
    {
        if (suppressCurrentUow || CurrentUnitOfWorks.IsEmpty()) CurrentUnitOfWorks.Add(CreateNewUow(false));

        return CurrentUow();
    }

    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IPlatformUnitOfWork
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

    public IPlatformUnitOfWork GlobalUow => globalUow ??= CreateNewUow(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void RemoveAllInactiveUow()
    {
        if (disposed || disposing || (CurrentUnitOfWorks.IsEmpty() && (!FreeCreatedUnitOfWorks.IsValueCreated || FreeCreatedUnitOfWorks.Value.IsEmpty))) return;

        List<IPlatformUnitOfWork> removedUOWs = [];
        try
        {
            RemoveAllInactiveUowLock.Wait();

            CurrentUnitOfWorks.RemoveWhere(p => !p.IsActive(), out removedUOWs);

            if (FreeCreatedUnitOfWorks.IsValueCreated)
                FreeCreatedUnitOfWorks.Value.Keys
                    .Where(key => !FreeCreatedUnitOfWorks.Value[key].IsActive())
                    .ForEach(
                        inactivatedUowKey =>
                        {
                            FreeCreatedUnitOfWorks.Value.TryRemove(inactivatedUowKey, out var removedUOW);
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

    public virtual IPlatformUnitOfWork CurrentOrCreatedUow(string uowId)
    {
        return LastOrDefaultMatchedUowOfId(CurrentUnitOfWorks, uowId) ??
               (FreeCreatedUnitOfWorks.IsValueCreated
                   ? LastOrDefaultMatchedUowOfId(FreeCreatedUnitOfWorks.Value.Values.ToList(), uowId)
                   : null);
    }

    public static IPlatformUnitOfWork LastOrDefaultMatchedUowOfId(List<IPlatformUnitOfWork> unitOfWorks, string uowId)
    {
        for (var i = unitOfWorks.Count - 1; i >= 0; i--)
        {
            var matchedUow = unitOfWorks.ElementAtOrDefault(i)?.UowOfId(uowId);

            if (matchedUow != null) return matchedUow;
        }

        return null;
    }

    private static void EnsureUowActive(IPlatformUnitOfWork currentUow)
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
            this.disposing = true;

            if (disposing)
            {
                // Release managed resources
                // ToList to clone the list to dispose because dispose could cause trigger RemoveAllInactiveUow => modified the original list
                CurrentUnitOfWorks.ToList().ForEach(currentUnitOfWork => currentUnitOfWork?.Dispose());
                CurrentUnitOfWorks.Clear();

                // Release managed resources
                // ToList to clone the list to dispose because dispose could cause trigger RemoveAllInactiveUow => modified the original list
                if (FreeCreatedUnitOfWorks.IsValueCreated)
                {
                    FreeCreatedUnitOfWorks.Value.ToList().ForEach(currentUnitOfWork => currentUnitOfWork.Value?.Dispose());
                    FreeCreatedUnitOfWorks.Value.Clear();
                }

                globalUow?.Dispose();
                globalUow = null;

                RemoveAllInactiveUowLock.Dispose();
            }

            // Release unmanaged resources

            disposed = true;
            this.disposing = false;
        }
    }

    ~PlatformUnitOfWorkManager()
    {
        Dispose(false);
    }
}
