using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

internal class TextSnippetRepository<TEntity>
    : PlatformEfCoreRepository<TEntity, Guid, TextSnippetDbContext>, ITextSnippetRepository<TEntity>
    where TEntity : class, IEntity<Guid>, new()
{
    public TextSnippetRepository(
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        DbContextOptions<TextSnippetDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        dbContextOptions,
        serviceProvider)
    {
    }
}

internal class TextSnippetRootRepository<TEntity>
    : PlatformEfCoreRootRepository<TEntity, Guid, TextSnippetDbContext>, ITextSnippetRootRepository<TEntity>
    where TEntity : class, IRootEntity<Guid>, new()
{
    public TextSnippetRootRepository(
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        DbContextOptions<TextSnippetDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        dbContextOptions,
        serviceProvider)
    {
    }

    // EXAMPLE SOME OVERRIDE TO USE FOR LEGACY CODE IF NEEDED
    //// Override ExecuteAutoOpenUowUsingOnceTimeForRead because this has legacy code. After migrated to platform, the old code might
    //// have place where in command it has unit of work, tracking entity to update and do not want query get data from separated uow
    //// if the ActiveUow existing
    //// TODO: Could refactor remove this override and test ensure that everything works, or fix any place that has error by calling repository create/update
    //// function to attach the entity
    protected override bool ForceUseSameCurrentActiveUowIfExistingForQuery => true;

    //protected override bool IsDefaultNoTrackingQuery =>true;
}
