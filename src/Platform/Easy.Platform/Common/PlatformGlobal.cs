using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common;

public abstract class PlatformGlobal
{
    public static IServiceProvider RootServiceProvider { get; private set; }

    public static ILoggerFactory LoggerFactory => RootServiceProvider.GetRequiredService<ILoggerFactory>();

    public static IConfiguration Configuration => RootServiceProvider.GetRequiredService<IConfiguration>();

    public static ILogger CreateDefaultLogger()
    {
        return CreateDefaultLogger(RootServiceProvider);
    }

    public static ILogger CreateDefaultLogger(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Easy.Platform");
    }

    public static void SetRootServiceProvider(IServiceProvider rootServiceProvider)
    {
        RootServiceProvider = rootServiceProvider;
    }
}
