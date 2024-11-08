using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Application;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Persistence.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    public async Task TryCurrentActiveUowSaveChangesAsync()
    {
        var currentActiveUow = TryGetCurrentActiveUow();

        if (currentActiveUow != null)
            await currentActiveUow.SaveChangesAsync();
    }

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

    public async Task ExecuteUowTask(Func<Task> taskFn)
    {
        var currentActiveUow = TryGetCurrentActiveUow();

        if (currentActiveUow != null)
        {
            await taskFn();
            await currentActiveUow.SaveChangesAsync();
        }
        else
        {
            using (var uow = Begin())
            {
                await taskFn();
                await uow.CompleteAsync();
            }
        }
    }

    public async Task<TResult> ExecuteUowTask<TResult>(Func<Task<TResult>> taskFn)
    {
        var currentActiveUow = TryGetCurrentActiveUow();

        if (currentActiveUow != null)
        {
            var result = await taskFn();
            await currentActiveUow.SaveChangesAsync();

            return result;
        }

        using (var uow = Begin())
        {
            var result = await taskFn();
            await uow.CompleteAsync();

            return result;
        }
    }

    IPlatformRootServiceProvider GetRootServiceProvider();

    /// <inheritdoc cref="DependencyInjectionExtension.ExecuteInjectScopedScrollingPagingAsync{TItem}(System.IServiceProvider,int,System.Delegate,object[])" />
    public async Task ExecuteInjectScopedScrollingPagingAsync<TItem>(
        Delegate method,
        int maxExecutionCount,
        params object[] manuallyParams)
    {
        await GetRootServiceProvider()
            .ExecuteInjectScopedScrollingPagingAsync<TItem>(
                maxExecutionCount,
                async (IPlatformUnitOfWorkManager newScopeUnitOfWorkManager, IServiceProvider serviceProvider) =>
                {
                    try
                    {
                        using (var uow = newScopeUnitOfWorkManager.Begin(false))
                        {
                            var result = await serviceProvider.ExecuteInjectAsync<List<TItem>>(method, manuallyParams ?? []);

                            await uow.CompleteAsync();

                            return result;
                        }
                    }
                    finally
                    {
                        GetRootServiceProvider().GetService<IPlatformApplicationSettingContext>().ProcessAutoGarbageCollect();
                    }
                });
    }

    /// <inheritdoc cref="DependencyInjectionExtension.ExecuteInjectScopedPagingAsync(System.IServiceProvider,long,int,System.Delegate,object[])" />
    public async Task ExecuteInjectScopedPagingAsync(
        long maxItemCount,
        int pageSize,
        Delegate method,
        params object[] manuallyParams)
    {
        await GetRootServiceProvider()
            .ExecuteInjectScopedPagingAsync(
                maxItemCount,
                pageSize,
                async (int skipCount, int pageSize, IPlatformUnitOfWorkManager newScopeUnitOfWorkManager, IServiceProvider serviceProvider) =>
                {
                    try
                    {
                        using (var uow = newScopeUnitOfWorkManager.Begin(false))
                        {
                            await serviceProvider.ExecuteInjectAsync(method, manuallyParams: new object[] { skipCount, pageSize }.Concat(manuallyParams ?? []).ToArray());

                            await uow.CompleteAsync();
                        }
                    }
                    finally
                    {
                        GetRootServiceProvider().GetService<IPlatformApplicationSettingContext>().ProcessAutoGarbageCollect();
                    }
                });
    }

    public async Task ExecuteInjectScopedAsync(
        Delegate method,
        params object[] manuallyParams)
    {
        await GetRootServiceProvider()
            .ExecuteInjectScopedAsync(
                async (IPlatformUnitOfWorkManager newScopeUnitOfWorkManager, IServiceProvider serviceProvider) =>
                {
                    try
                    {
                        using (var uow = newScopeUnitOfWorkManager.Begin(false))
                        {
                            await serviceProvider.ExecuteInjectAsync(method, manuallyParams);

                            await uow.CompleteAsync();
                        }
                    }
                    finally
                    {
                        GetRootServiceProvider().GetService<IPlatformApplicationSettingContext>().ProcessAutoGarbageCollect();
                    }
                });
    }

    public async Task<TResult> ExecuteInjectScopedAsync<TResult>(
        Delegate method,
        params object[] manuallyParams)
    {
        return await GetRootServiceProvider()
            .ExecuteInjectScopedAsync<TResult>(
                async (IPlatformUnitOfWorkManager newScopeUnitOfWorkManager, IServiceProvider serviceProvider) =>
                {
                    try
                    {
                        using (var uow = newScopeUnitOfWorkManager.Begin(false))
                        {
                            var result = await serviceProvider.ExecuteInjectAsync<TResult>(method, manuallyParams);

                            await uow.CompleteAsync();

                            return result;
                        }
                    }
                    finally
                    {
                        GetRootServiceProvider().GetService<IPlatformApplicationSettingContext>().ProcessAutoGarbageCollect();
                    }
                });
    }
}

public abstract class PlatformUnitOfWorkManager : IPlatformUnitOfWorkManager
{
    private readonly Lazy<ConcurrentDictionary<string, IPlatformUnitOfWork>> completedUnitOfWorksDictLazy =
        new(() => new ConcurrentDictionary<string, IPlatformUnitOfWork>());

    private readonly Lazy<IPlatformCqrs> currentSameScopeCqrsLazy;

    private readonly Lazy<ConcurrentDictionary<string, IPlatformUnitOfWork>> currentUnitOfWorksDictLazy =
        new(() => new ConcurrentDictionary<string, IPlatformUnitOfWork>());

    private readonly Lazy<ConcurrentDictionary<string, IPlatformUnitOfWork>> freeCreatedUnitOfWorksLazy =
        new(() => new ConcurrentDictionary<string, IPlatformUnitOfWork>());

    private readonly Lazy<IPlatformUnitOfWork> globalUowLazy;

    private readonly Lazy<ConcurrentDictionary<string, ConcurrentDictionary<string, IPlatformUnitOfWork>>> lastOrDefaultMatchedUowOfIdCachedResultDictLazy =
        new(() => new ConcurrentDictionary<string, ConcurrentDictionary<string, IPlatformUnitOfWork>>());

    private volatile IPlatformUnitOfWork? cachedCurrentUow;
    private bool disposed;

    protected PlatformUnitOfWorkManager(Lazy<IPlatformCqrs> cqrs, IPlatformRootServiceProvider rootServiceProvider, IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        RootServiceProvider = rootServiceProvider;
        currentSameScopeCqrsLazy = cqrs;
        globalUowLazy = new Lazy<IPlatformUnitOfWork>(() => CreateNewUow(false));
    }

    protected ConcurrentDictionary<string, IPlatformUnitOfWork> CompletedUows => completedUnitOfWorksDictLazy.Value;
    protected ConcurrentDictionary<string, IPlatformUnitOfWork> CurrentUnitOfWorksDict => currentUnitOfWorksDictLazy.Value;
    protected ConcurrentDictionary<string, IPlatformUnitOfWork> FreeCreatedUnitOfWorks => freeCreatedUnitOfWorksLazy.Value;
    protected IPlatformRootServiceProvider RootServiceProvider { get; }
    protected IServiceProvider ServiceProvider { get; }

    public IPlatformCqrs CurrentSameScopeCqrs => currentSameScopeCqrsLazy.Value;

    public virtual IPlatformUnitOfWork CreateNewUow(bool isUsingOnceTransientUow)
    {
        // Doing create scope because IUnitOfWork resolve with DbContext, and DbContext lifetime is usually scoped to support resolve db context
        // to use it directly in application layer in some project or cases without using repository.
        // But we still want to support Uow create new like transient, each uow associated with new db context
        // So that we can begin/destroy uow separately
        var newScope = ServiceProvider.CreateScope();

        var uow = new PlatformAggregatedPersistenceUnitOfWork(
                RootServiceProvider,
                newScope.ServiceProvider,
                newScope.ServiceProvider.GetService<ILoggerFactory>())
            .With(
                p =>
                {
                    p.AssociatedToDisposeWithServiceScope = newScope;
                    p.IsUsingOnceTransientUow = isUsingOnceTransientUow;
                    p.CreatedByUnitOfWorkManager = this;
                });

        if (isUsingOnceTransientUow)
        {
            FreeCreatedUnitOfWorks.TryAdd(uow.Id, uow);
            uow.OnUowCompletedActions.Add(
                () => Task.Run(
                    () =>
                    {
                        FreeCreatedUnitOfWorks.TryRemove(uow.Id, out _);
                        CompletedUows.TryAdd(uow.Id, uow);
                    }));
            uow.OnDisposedActions.Add(
                () => Task.Run(
                    () =>
                    {
                        FreeCreatedUnitOfWorks.TryRemove(uow.Id, out _);
                        CompletedUows.TryRemove(uow.Id, out _);
                    }));
        }

        return uow;
    }

    public virtual IPlatformUnitOfWork? CurrentUow()
    {
        if (CurrentUnitOfWorksDict.IsEmpty) return null;
        if (cachedCurrentUow != null) return cachedCurrentUow;

        cachedCurrentUow = CurrentUnitOfWorksDict.Count == 1 ? CurrentUnitOfWorksDict.First().Value : CurrentUnitOfWorksDict.MaxBy(p => p.Value.BeginOrder).Value;

        return cachedCurrentUow;
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
        return CurrentUow().Pipe(p => p?.IsActive() == true ? p : null);
    }

    public IPlatformUnitOfWork? TryGetCurrentOrCreatedActiveUow(string uowId)
    {
        if (uowId == null) return TryGetCurrentActiveUow();

        var currentOrCreatedUow = CurrentOrCreatedUow(uowId);

        return currentOrCreatedUow?.IsActive() == true ? currentOrCreatedUow : null;
    }

    public IPlatformUnitOfWork? TryGetCurrentActiveUow(string uowId)
    {
        if (uowId == null) return TryGetCurrentActiveUow();

        var currentUow = LastOrDefaultMatchedUowOfId(nameof(CurrentUnitOfWorksDict), CurrentUnitOfWorksDict, uowId);

        return currentUow?.IsActive() == true ? currentUow : null;
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
        if (suppressCurrentUow || CurrentUnitOfWorksDict.IsEmpty())
        {
            var newUow = CreateNewUow(false)
                .With(p => p.BeginOrder = CurrentUnitOfWorksDict.Count);

            CurrentUnitOfWorksDict.TryAdd(newUow.Id, newUow);
            cachedCurrentUow = newUow;

            newUow.OnUowCompletedActions.Add(
                () => Task.Run(
                    () =>
                    {
                        cachedCurrentUow = null;
                        CurrentUnitOfWorksDict.TryRemove(newUow.Id, out _);
                        CompletedUows.TryAdd(newUow.Id, newUow);
                    }));
            newUow.OnDisposedActions.Add(
                () => Task.Run(
                    () =>
                    {
                        cachedCurrentUow = null;
                        CurrentUnitOfWorksDict.TryRemove(newUow.Id, out _);
                        CompletedUows.TryRemove(newUow.Id, out _);
                    }));

            return newUow;
        }

        return CurrentUow();
    }

    public IPlatformRootServiceProvider GetRootServiceProvider()
    {
        return RootServiceProvider;
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

    public IPlatformUnitOfWork GlobalUow => globalUowLazy.Value;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual IPlatformUnitOfWork? CurrentOrCreatedUow(string uowId)
    {
        return
            CurrentUnitOfWorksDict.GetValueOrDefault(uowId) ??
            FreeCreatedUnitOfWorks.GetValueOrDefault(uowId) ??
            LastOrDefaultMatchedUowOfId(nameof(CurrentUnitOfWorksDict), CurrentUnitOfWorksDict, uowId) ??
            LastOrDefaultMatchedUowOfId(nameof(FreeCreatedUnitOfWorks), FreeCreatedUnitOfWorks, uowId);
    }

    public IPlatformUnitOfWork LastOrDefaultMatchedUowOfId(
        string unitOfWorksDictCacheKey,
        ConcurrentDictionary<string, IPlatformUnitOfWork> unitOfWorksDict,
        string uowId)
    {
        return lastOrDefaultMatchedUowOfIdCachedResultDictLazy.Value
            .GetOrAdd(
                unitOfWorksDictCacheKey,
                _ => new ConcurrentDictionary<string, IPlatformUnitOfWork>())
            .GetOrAdd(
                uowId,
                uowId =>
                {
                    var unitOfWorksValues = unitOfWorksDict.Values;

                    for (var i = unitOfWorksValues.Count - 1; i >= 0; i--)
                    {
                        var matchedUow = unitOfWorksValues.ElementAtOrDefault(i)?.UowOfId(uowId);

                        if (matchedUow != null)
                            return matchedUow;
                    }

                    return null;
                });
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
            if (disposing)
            {
                // Release managed resources

                if (currentUnitOfWorksDictLazy.IsValueCreated)
                {
                    CurrentUnitOfWorksDict.Values.ForEach(p => p?.Dispose());
                    currentUnitOfWorksDictLazy.Value.Clear();
                }

                if (freeCreatedUnitOfWorksLazy.IsValueCreated)
                {
                    FreeCreatedUnitOfWorks.Values.ForEach(p => p?.Dispose());
                    freeCreatedUnitOfWorksLazy.Value.Clear();
                }

                if (lastOrDefaultMatchedUowOfIdCachedResultDictLazy.IsValueCreated)
                    lastOrDefaultMatchedUowOfIdCachedResultDictLazy.Value.Clear();

                CompletedUows.Values.ForEach(p => p.Dispose());
                CompletedUows.Clear();

                if (globalUowLazy.IsValueCreated)
                    globalUowLazy.Value.Dispose();
            }

            // Release unmanaged resources

            disposed = true;
        }
    }
}
