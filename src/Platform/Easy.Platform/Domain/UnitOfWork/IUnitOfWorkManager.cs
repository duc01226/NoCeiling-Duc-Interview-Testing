using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.UnitOfWork;

/// <summary>
/// Unit of work manager.
/// Used to begin and control a unit of work.
/// </summary>
public interface IUnitOfWorkManager : IDisposable
{
    /// <summary>
    /// A single separated global uow in current scoped is used by repository for read data using query, usually when need to return data
    /// as enumerable to help download data like streaming data (not load all big data into ram) <br />
    /// or any other purpose that just want to using query directly without think about uow of the query. <br />
    /// This uow is auto created once per scope when access it. <br />
    /// This won't affect the normal current uow queue list when Begin a new uow.
    /// </summary>
    public IUnitOfWork GlobalUow { get; }

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
    /// Gets currently latest active unit of work of type <see cref="TUnitOfWork" />.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork;

    /// <summary>
    /// Gets currently latest active unit of work. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork TryGetCurrentActiveUow();

    /// <summary>
    /// Check that is there any currently latest active unit of work
    /// </summary>
    public bool HasCurrentActiveUow();

    /// <summary>
    /// Begin a new last registered unit of work.
    /// If current active unit of work existed, return it.
    /// </summary>
    /// <param name="suppressCurrentUow">
    /// When true, new uow will be created event if current uow existed. When false, use
    /// current active uow if possible. Default is true.
    /// </param>
    public IUnitOfWork Begin(bool suppressCurrentUow = true);
}

public abstract class PlatformUnitOfWorkManager : IUnitOfWorkManager
{
    protected readonly List<IUnitOfWork> CurrentUnitOfWorks = new();

    private IUnitOfWork globalUow;

    public abstract IUnitOfWork CreateNewUow();

    public virtual IUnitOfWork CurrentUow()
    {
        RemoveAllInactiveUow();

        return CurrentUnitOfWorks.LastOrDefault();
    }

    public IUnitOfWork CurrentActiveUow()
    {
        return CurrentUow()
            .Ensure(
                must: currentUow => currentUow != null,
                "There's no current any uow has been begun.")
            .Ensure(
                must: currentUow => currentUow.IsActive(),
                "Current unit of work has been completed or disposed.");
    }

    public IUnitOfWork TryGetCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true
            ? CurrentUow()
            : null;
    }

    public bool HasCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true;
    }

    public virtual IUnitOfWork Begin(bool suppressCurrentUow = true)
    {
        RemoveAllInactiveUow();

        if (suppressCurrentUow || CurrentUnitOfWorks.IsEmpty()) CurrentUnitOfWorks.Add(CreateNewUow());

        return CurrentUow();
    }

    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        return CurrentUow().UowOfType<TUnitOfWork>()
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

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // free managed resources
            CurrentUnitOfWorks.ForEach(currentUnitOfWork => currentUnitOfWork.Dispose());
            CurrentUnitOfWorks.Clear();
            globalUow?.Dispose();
        }
    }

    protected List<IUnitOfWork> RemoveAllInactiveUow()
    {
        CurrentUnitOfWorks.RemoveWhere(p => !p.IsActive(), out _);

        return CurrentUnitOfWorks;
    }
}
