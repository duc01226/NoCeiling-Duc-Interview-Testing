using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.Repositories;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo;

internal sealed class TextSnippetMultiDbDemoRepository<TEntity>
    : PlatformMongoDbRepository<TEntity, Guid, TextSnippetMultiDbDemoDbContext>,
        ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<Guid>, new()
{
    public TextSnippetMultiDbDemoRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}

internal sealed class TextSnippetMultiDbDemoRootRepository<TEntity>
    : PlatformMongoDbRootRepository<TEntity, Guid, TextSnippetMultiDbDemoDbContext>,
        ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<Guid>, new()
{
    public TextSnippetMultiDbDemoRootRepository(IUnitOfWorkManager unitOfWorkManager, IPlatformCqrs cqrs, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
    }
}
