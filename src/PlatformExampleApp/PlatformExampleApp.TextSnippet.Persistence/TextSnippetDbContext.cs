using Easy.Platform.Common.Cqrs;
using Easy.Platform.EfCore;
using Easy.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Persistence;

namespace PlatformExampleApp.TextSnippet.Persistence;

public class TextSnippetDbContext : PlatformEfCoreDbContext<TextSnippetDbContext>, ITextSnippetDbContext
{
    public TextSnippetDbContext(
        DbContextOptions<TextSnippetDbContext> options,
        ILoggerFactory loggerFactory,
        IPlatformCqrs cqrs,
        PlatformPersistenceConfiguration<TextSnippetDbContext> persistenceConfiguration) : base(options, loggerFactory, cqrs, persistenceConfiguration)
    {
    }

    /// <summary>
    /// We use IDesignTimeDbContextFactory to help use "dotnet ef migrations add" at this project rather than at api project
    /// which we couldn't do it because we are implementing switch db
    /// References: https://docs.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-a-design-time-factory
    /// </summary>
    public class TextSnippetDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TextSnippetDbContext>
    {
        public TextSnippetDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TextSnippetDbContext>();
            optionsBuilder.UseSqlServer("Data Source=localhost,14330;Initial Catalog=TextSnippedDb;User ID=sa;Password=123456Abc; Encrypt=False;");

            return new TextSnippetDbContext(optionsBuilder.Options, new LoggerFactory(), null, new PlatformPersistenceConfiguration<TextSnippetDbContext>());
        }
    }

    // Override this in case you have two db context in same project, you dont want it to scan and apply entity configuration conflicted with each others
    //protected override List<Type> ApplyForLimitedEntityTypes()
    //{
    //    return Util.ListBuilder.New(
    //        typeof(Your Limited entity type for the db context to auto run entity configuration by scanning assembly));
    //}
}
