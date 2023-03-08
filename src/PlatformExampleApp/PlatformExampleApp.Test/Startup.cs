using Easy.Platform.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PlatformExampleApp.Test;

public class Startup : BaseStartup
{
    // Uncomment this code, change the example "fallbackAspCoreEnv: "Development.Docker"" to the specific environment to run test in visual studio
    // Because when you click run in visual studio, ASPNETCORE_ENVIRONMENT is missing which
    // will fallback to fallbackAspCoreEnv value
    public override void ConfigureHostConfiguration(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureHostConfiguration(
            builder => builder.AddConfiguration(
                PlatformConfigurationBuilder.GetConfigurationBuilder(fallbackAspCoreEnv: "Development").Build()));
    }

    // Optional override to config WebDriverManager DriverOptions
    public override void ConfigWebDriverOptions(IOptions options)
    {
        options.Timeouts().PageLoad = 1.Minutes();
    }
}
