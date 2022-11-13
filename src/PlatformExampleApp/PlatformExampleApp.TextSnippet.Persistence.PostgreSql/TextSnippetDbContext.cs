using Easy.Platform.Common.Cqrs;
using Easy.Platform.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

public class TextSnippetDbContext : PlatformEfCoreDbContext<TextSnippetDbContext>
{
    public TextSnippetDbContext(DbContextOptions<TextSnippetDbContext> options, ILoggerFactory loggerFactory, IPlatformCqrs cqrs) : base(options, loggerFactory, cqrs)
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
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=TextSnippedDb");

            return new TextSnippetDbContext(optionsBuilder.Options, new LoggerFactory(), null);
        }
    }
}
