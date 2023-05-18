using Easy.Platform.Common.Logging.BackgroundThreadFullStackTrace;
using Serilog;

namespace Easy.Platform.Common.Logging;

public static class PlatformLoggerConfigurationExtensions
{
    public static LoggerConfiguration ApplyDefaultPlatformConfiguration(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration.Enrich.With(new PlatformBackgroundThreadFullStackTraceEnricher());
    }
}
