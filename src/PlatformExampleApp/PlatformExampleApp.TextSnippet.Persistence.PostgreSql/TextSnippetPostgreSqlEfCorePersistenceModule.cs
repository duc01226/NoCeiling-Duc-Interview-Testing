using System.Linq.Expressions;
using Easy.Platform.EfCore;
using Easy.Platform.EfCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

namespace PlatformExampleApp.TextSnippet.Persistence;

public class TextSnippetPostgreSqlEfCorePersistenceModule : PlatformEfCorePersistenceModule<TextSnippetDbContext>
{
    public TextSnippetPostgreSqlEfCorePersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    // Override using fulltext search index for BETTER PERFORMANCE
    protected override EfCorePlatformFullTextSearchPersistenceService FullTextSearchPersistenceServiceProvider(IServiceProvider serviceProvider)
    {
        return new TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService(serviceProvider);
    }

    protected override bool EnableInboxBusMessage()
    {
        return true;
    }

    protected override bool EnableOutboxBusMessage()
    {
        return true;
    }

    protected override Action<DbContextOptionsBuilder> DbContextOptionsBuilderActionProvider(
        IServiceProvider serviceProvider)
    {
        return options =>
            options.UseNpgsql(Configuration.GetConnectionString("PostgreSqlConnection"));
    }
}

public class TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService : EfCorePlatformFullTextSearchPersistenceService
{
    public TextSnippetPostgreSqlEfCorePlatformFullTextSearchPersistenceService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public static Expression<Func<TEntity, bool>> BuildPostgreSqlFullTextSearchPropPredicate<TEntity>(string fullTextSearchPropName, string searchWord)
    {
        return entity => EF.Functions.ToTsVector(EF.Property<string>(entity, fullTextSearchPropName)).Matches(searchWord);
    }

    protected override Expression<Func<TEntity, bool>> BuildFullTextSearchPropPredicate<TEntity>(string fullTextSearchPropName, string searchWord)
    {
        return BuildPostgreSqlFullTextSearchPropPredicate<TEntity>(fullTextSearchPropName, searchWord);
    }
}
