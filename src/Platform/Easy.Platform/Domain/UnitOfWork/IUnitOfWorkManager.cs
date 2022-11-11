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
    /// Just create and return a new instance of uow without manage it
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
    /// Gets currently latest active unit of work. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork TryGetCurrentActiveUow();

    /// <summary>
    /// Check that is there any currently latest active unit of work
    /// </summary>
    public bool HasCurrentActive();

    /// <summary>
    /// Begin a new last registered unit of work.
    /// If current active unit of work existed, return it.
    /// </summary>
    /// <param name="suppressCurrentUow">
    /// When true, new uow will be created event if current uow existed. When false, use
    /// current active uow if possible. Default is true.
    /// </param>
    public IUnitOfWork Begin(bool suppressCurrentUow = true);

    /// <summary>
    /// Gets last begun inner unit of work of type <see cref="TUnitOfWork" /> (or null if not exists).
    /// </summary>
    [return: MaybeNull]
    public TUnitOfWork CurrentUowInner<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork;

    /// <summary>
    /// Gets currently latest active inner unit of work of type <see cref="TUnitOfWork" />.
    /// <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public TUnitOfWork CurrentInnerActiveUow<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork;

    public IUnitOfWork CurrentReadonlyDataEnumerableUow();
}

public abstract class PlatformUnitOfWorkManager : IUnitOfWorkManager
{
    protected readonly List<IUnitOfWork> CurrentUnitOfWorks = new();

    private IUnitOfWork currentReadonlyDataEnumerableUow;
    private bool isDisposed;

    public abstract IUnitOfWork CreateNewUow();

    public virtual IUnitOfWork CurrentUow()
    {
        RemoveAllLastInactiveUow();

        return CurrentUnitOfWorks.LastOrDefault();
    }

    public IUnitOfWork CurrentActiveUow()
    {
        return CurrentUow()
            .Ensure(
                must: currentUow => currentUow?.IsActive() == true,
                "Current active unit of work is missing.");
    }

    public IUnitOfWork TryGetCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true
            ? CurrentUow()
            : null;
    }

    public bool HasCurrentActive()
    {
        return CurrentUow()?.IsActive() == true;
    }

    public virtual IUnitOfWork Begin(bool suppressCurrentUow = true)
    {
        RemoveAllLastInactiveUow();

        if (suppressCurrentUow || CurrentUnitOfWorks.IsEmpty()) CurrentUnitOfWorks.Add(CreateNewUow());

        return CurrentUow();
    }

    public TUnitOfWork CurrentUowInner<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        return CurrentUow()?.CurrentInner<TUnitOfWork>();
    }

    public TUnitOfWork CurrentInnerActiveUow<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        return CurrentUowInner<TUnitOfWork>()
            .Ensure(
                must: currentInnerUow => currentInnerUow?.IsActive() == true,
                $"Current active inner unit of work of type {typeof(TUnitOfWork).FullName} is missing. Should use {nameof(IUnitOfWorkManager)} to Begin a new UOW.");
    }

    public IUnitOfWork CurrentReadonlyDataEnumerableUow()
    {
        return currentReadonlyDataEnumerableUow ??= CreateNewUow();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
            return;

        if (disposing)
        {
            // free managed resources
            CurrentUnitOfWorks.ForEach(currentUnitOfWork => currentUnitOfWork.Dispose());
            CurrentUnitOfWorks.Clear();
            currentReadonlyDataEnumerableUow?.Dispose();
        }

        isDisposed = true;
    }

    protected List<IUnitOfWork> RemoveAllLastInactiveUow()
    {
        CurrentUnitOfWorks.RemoveWhere(p => !p.IsActive(), out _);

        return CurrentUnitOfWorks;
    }
}
