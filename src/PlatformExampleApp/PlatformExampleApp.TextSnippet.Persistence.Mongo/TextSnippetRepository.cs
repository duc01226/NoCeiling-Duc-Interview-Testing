using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.Repositories;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo;

internal sealed class TextSnippetRepository<TEntity> : PlatformMongoDbRepository<TEntity, string, TextSnippetDbContext>, ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<string>, new()
{
    public TextSnippetRepository(IServiceProvider serviceProvider) : base(
        serviceProvider)
    {
    }
}

internal sealed class TextSnippetRootRepository<TEntity> : PlatformMongoDbRootRepository<TEntity, string, TextSnippetDbContext>, ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<string>, new()
{
    public TextSnippetRootRepository(IServiceProvider serviceProvider) : base(
        serviceProvider)
    {
    }
}
