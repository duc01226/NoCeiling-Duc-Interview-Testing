using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

internal sealed class TextSnippetRepository<TEntity>
    : PlatformEfCoreRepository<TEntity, string, TextSnippetDbContext>, ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<string>, new()
{
    public TextSnippetRepository(
        DbContextOptions<TextSnippetDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        dbContextOptions,
        serviceProvider)
    {
    }
}

internal sealed class TextSnippetRootRepository<TEntity>
    : PlatformEfCoreRootRepository<TEntity, string, TextSnippetDbContext>, ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<string>, new()
{
    public TextSnippetRootRepository(
        DbContextOptions<TextSnippetDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        dbContextOptions,
        serviceProvider)
    {
    }
}
