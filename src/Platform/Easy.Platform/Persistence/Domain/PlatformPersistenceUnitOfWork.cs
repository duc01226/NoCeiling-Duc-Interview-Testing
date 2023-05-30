using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Persistence.Domain;

public interface IPlatformPersistenceUnitOfWork<out TDbContext> : IUnitOfWork
    where TDbContext : IPlatformDbContext
{
    public TDbContext DbContext { get; }
}

public abstract class PlatformPersistenceUnitOfWork<TDbContext> : PlatformUnitOfWork, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    public PlatformPersistenceUnitOfWork(TDbContext dbContext)
    {
        DbContext = dbContext.With(_ => _.MappedUnitOfWork = this);
    }

    public TDbContext DbContext { get; }

    // Protected implementation of Dispose pattern.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Dispose managed state (managed objects).
        if (disposing)
            DbContext?.Dispose();

        Disposed = true;
    }

    protected override Dictionary<string, object> BuildDataContextBeforeExecuteOnCompletedActionsInBackground()
    {
        var currentUserContextAllValues =
            PlatformGlobal.RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>().Current.GetAllKeyValues();

        return currentUserContextAllValues;
    }

    protected override void ReApplyDataContextInNewBackgroundThreadExecution(Dictionary<string, object> dataContextBeforeNewScopeExecution)
    {
        if (dataContextBeforeNewScopeExecution != null)
            PlatformGlobal.RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>().Current.SetValues(dataContextBeforeNewScopeExecution);
    }
}
