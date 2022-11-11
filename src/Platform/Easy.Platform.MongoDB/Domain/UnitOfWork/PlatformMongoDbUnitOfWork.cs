using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.MongoDB.Domain.UnitOfWork;

public interface IPlatformMongoDbUnitOfWork<out TDbContext> : IUnitOfWork
    where TDbContext : IPlatformMongoDbContext<TDbContext>
{
    public TDbContext DbContext { get; }
}

public class PlatformMongoDbUnitOfWork<TDbContext>
    : PlatformUnitOfWork<TDbContext>, IPlatformMongoDbUnitOfWork<TDbContext> where TDbContext : IPlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbUnitOfWork(TDbContext dbContext) : base(dbContext)
    {
    }

    public override bool IsNoTransactionUow()
    {
        return true;
    }
}
