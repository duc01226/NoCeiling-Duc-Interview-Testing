using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Domain.Repositories;

public interface ITextSnippetRepository<TEntity> : IPlatformQueryableRepository<TEntity, string>
    where TEntity : class, IEntity<string>, new()
{
}

public interface ITextSnippetRootRepository<TEntity> : IPlatformQueryableRootRepository<TEntity, string>, ITextSnippetRepository<TEntity>
    where TEntity : class, IRootEntity<string>, new()
{
}
