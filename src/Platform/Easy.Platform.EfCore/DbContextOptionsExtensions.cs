using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Proxies.Internal;

namespace Easy.Platform.EfCore;

public static class DbContextOptionsExtensions
{
    public static bool IsUsingLazyLoadingProxy<TDbContext>(this DbContextOptions<TDbContext> contextOptions) where TDbContext : DbContext
    {
        return contextOptions.FindExtension<ProxiesOptionsExtension>() != null;
    }
}
