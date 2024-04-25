using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

internal sealed class TextSnippetRepository<TEntity>
    : PlatformEfCoreRepository<TEntity, Guid, TextSnippetDbContext>, ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<Guid>, new()
{
    public TextSnippetRepository(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        DbContextOptions<TextSnippetDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        dbContextOptions,
        serviceProvider)
    {
    }
}

internal sealed class TextSnippetRootRepository<TEntity>
    : PlatformEfCoreRootRepository<TEntity, Guid, TextSnippetDbContext>, ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<Guid>, new()
{
    public TextSnippetRootRepository(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        DbContextOptions<TextSnippetDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        dbContextOptions,
        serviceProvider)
    {
    }
}
