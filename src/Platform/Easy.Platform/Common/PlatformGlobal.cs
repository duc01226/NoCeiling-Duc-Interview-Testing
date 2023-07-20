using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common;

public abstract class PlatformGlobal
{
    /// <summary>
    /// This by default will be set as root service provider of the application on application module init
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; }

    public static ILoggerFactory LoggerFactory => ServiceProvider.GetRequiredService<ILoggerFactory>();

    public static IConfiguration Configuration => ServiceProvider.GetRequiredService<IConfiguration>();

    public static IPlatformApplicationUserContextAccessor UserContext => ServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>();

    public static IPlatformCacheRepositoryProvider CacheRepositoryProvider => ServiceProvider.GetRequiredService<IPlatformCacheRepositoryProvider>();

    public static ILogger CreateDefaultLogger()
    {
        return CreateDefaultLogger(ServiceProvider);
    }

    public static ILogger CreateDefaultLogger(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Easy.Platform");
    }

    public static void SetRootServiceProvider(IServiceProvider rootServiceProvider)
    {
        ServiceProvider = rootServiceProvider;
    }
}
