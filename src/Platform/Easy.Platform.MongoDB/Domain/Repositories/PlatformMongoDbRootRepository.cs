using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;

namespace Easy.Platform.MongoDB.Domain.Repositories;

public abstract class PlatformMongoDbRootRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformMongoDbRepository<TEntity, TPrimaryKey, TDbContext>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TDbContext : IPlatformMongoDbContext<TDbContext>
{
    public PlatformMongoDbRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(unitOfWorkManager, cqrs, serviceProvider)
    {
    }
}

public class PlatformDefaultMongoDbRootRepository<TEntity, TPrimaryKey, TDbContext> : PlatformMongoDbRootRepository<TEntity, TPrimaryKey, TDbContext>
    where TEntity : RootEntity<TEntity, TPrimaryKey>, new()
    where TDbContext : IPlatformMongoDbContext<TDbContext>
{
    public PlatformDefaultMongoDbRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(unitOfWorkManager, cqrs, serviceProvider)
    {
    }
}
