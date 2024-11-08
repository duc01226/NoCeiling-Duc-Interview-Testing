using Easy.Platform.Common;
using Easy.Platform.Persistence.Domain;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.MongoDB.Domain.UnitOfWork;

public interface IPlatformMongoDbPersistenceUnitOfWork<out TDbContext> : IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}

public class PlatformMongoDbPersistenceUnitOfWork<TDbContext>
    : PlatformPersistenceUnitOfWork<TDbContext>, IPlatformMongoDbPersistenceUnitOfWork<TDbContext> where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbPersistenceUnitOfWork(
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : base(
        rootServiceProvider,
        serviceProvider,
        loggerFactory)
    {
    }

    public override bool IsPseudoTransactionUow()
    {
        return true;
    }

    public override bool MustKeepUowForQuery()
    {
        return false;
    }
}
