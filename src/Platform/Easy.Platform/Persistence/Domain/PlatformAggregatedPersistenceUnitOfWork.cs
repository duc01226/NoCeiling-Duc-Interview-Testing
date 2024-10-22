using System.Collections.Concurrent;
using Easy.Platform.Common;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Persistence.Domain;

public interface IPlatformAggregatedPersistenceUnitOfWork : IPlatformUnitOfWork
{
    public bool IsPseudoTransactionUow<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IPlatformUnitOfWork;
    public bool MustKeepUowForQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IPlatformUnitOfWork;
    public bool DoesSupportParallelQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IPlatformUnitOfWork;
}

/// <summary>
/// The aggregated unit of work is to support multi database type in a same application.
/// Each item in InnerUnitOfWorks present a REAL unit of work including a db context
/// </summary>
public class PlatformAggregatedPersistenceUnitOfWork : PlatformUnitOfWork, IPlatformAggregatedPersistenceUnitOfWork
{
    private readonly Lazy<ConcurrentDictionary<string, IPlatformUnitOfWork>> cachedInnerUowByIdsLazy = new(() => new ConcurrentDictionary<string, IPlatformUnitOfWork>());
    private readonly Lazy<ConcurrentDictionary<Type, IPlatformUnitOfWork>> cachedInnerUowsLazy = new(() => new ConcurrentDictionary<Type, IPlatformUnitOfWork>());

    public PlatformAggregatedPersistenceUnitOfWork(
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : base(rootServiceProvider, serviceProvider, loggerFactory)
    {
    }

    protected override ConcurrentDictionary<Type, IPlatformUnitOfWork> CachedInnerUows => cachedInnerUowsLazy.Value;
    protected override ConcurrentDictionary<string, IPlatformUnitOfWork> CachedInnerUowByIds => cachedInnerUowByIdsLazy.Value;
    protected override ConcurrentDictionary<string, object>? CachedExistingOriginalEntities { get; } = null;

    public override bool IsPseudoTransactionUow()
    {
        return CachedInnerUows!.Values.All(p => p.IsPseudoTransactionUow());
    }

    public override bool MustKeepUowForQuery()
    {
        return CachedInnerUows!.Values.Any(p => p.MustKeepUowForQuery());
    }

    public override bool DoesSupportParallelQuery()
    {
        return CachedInnerUows!.Values.All(p => p.DoesSupportParallelQuery());
    }

    public bool IsPseudoTransactionUow<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IPlatformUnitOfWork
    {
        return CachedInnerUowByIds!.GetValueOrDefault(uow.Id)?.IsPseudoTransactionUow() == true;
    }

    public bool MustKeepUowForQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IPlatformUnitOfWork
    {
        return CachedInnerUowByIds!.GetValueOrDefault(uow.Id)?.MustKeepUowForQuery() == true;
    }

    public bool DoesSupportParallelQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IPlatformUnitOfWork
    {
        return CachedInnerUowByIds!.GetValueOrDefault(uow.Id)?.DoesSupportParallelQuery() == true;
    }

    protected override Task InternalSaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
