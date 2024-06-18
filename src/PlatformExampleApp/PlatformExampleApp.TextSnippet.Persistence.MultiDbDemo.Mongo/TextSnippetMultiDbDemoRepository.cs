using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.Repositories;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo;

internal sealed class TextSnippetMultiDbDemoRepository<TEntity>
    : PlatformMongoDbRepository<TEntity, string, TextSnippetMultiDbDemoDbContext>, ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<string>, new()
{
    public TextSnippetMultiDbDemoRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
    }
}

internal sealed class TextSnippetMultiDbDemoRootRepository<TEntity>
    : PlatformMongoDbRootRepository<TEntity, string, TextSnippetMultiDbDemoDbContext>, ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<string>, new()
{
    public TextSnippetMultiDbDemoRootRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
    }
}
