using Easy.Platform.Persistence.Domain;

namespace Easy.Platform.MongoDB.Domain.UnitOfWork;

public interface IPlatformMongoDbPersistenceUnitOfWork<out TDbContext> : IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}

public class PlatformMongoDbPersistenceUnitOfWork<TDbContext>
    : PlatformPersistenceUnitOfWork<TDbContext>, IPlatformMongoDbPersistenceUnitOfWork<TDbContext> where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbPersistenceUnitOfWork(TDbContext dbContext) : base(dbContext)
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

    public override bool DoesSupportParallelQuery()
    {
        return true;
    }
}
