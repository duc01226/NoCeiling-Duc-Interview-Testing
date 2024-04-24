using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.Repositories;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo;

internal sealed class TextSnippetRepository<TEntity> : PlatformMongoDbRepository<TEntity, Guid, TextSnippetDbContext>, ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<Guid>, new()
{
    public TextSnippetRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}

internal sealed class TextSnippetRootRepository<TEntity> : PlatformMongoDbRootRepository<TEntity, Guid, TextSnippetDbContext>, ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<Guid>, new()
{
    public TextSnippetRootRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}
