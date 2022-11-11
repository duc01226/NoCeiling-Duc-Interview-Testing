using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Domain.Repositories;

public interface ITextSnippetRepository<TEntity> : IPlatformQueryableRepository<TEntity, Guid>
    where TEntity : class, IEntity<Guid>, new()
{
}

public interface ITextSnippetRootRepository<TEntity> : IPlatformQueryableRootRepository<TEntity, Guid>,
    ITextSnippetRepository<TEntity>
    where TEntity : class, IRootEntity<Guid>, new()
{
}
