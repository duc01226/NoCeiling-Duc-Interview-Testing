using Serilog.Core;
using Serilog.Events;

namespace Easy.Platform.Common.Logging.BackgroundThreadFullStackTrace;

public class PlatformBackgroundThreadFullStackTraceEnricher : ILogEventEnricher
{
    public const string PlatformBackgroundThreadFullStackTraceLogPropertyName = "PlatformBackgroundThreadFullStackTrace";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (PlatformLogger.BackgroundThreadFullStackTraceContextAccessor.Current != null && logEvent.Exception != null)
        {
            var enrichProperty = propertyFactory
                .CreateProperty(
                    PlatformBackgroundThreadFullStackTraceLogPropertyName,
                    $"PlatformBackgroundThreadFullStackTrace: {PlatformLogger.BackgroundThreadFullStackTraceContextAccessor.Current}");

            logEvent.AddOrUpdateProperty(enrichProperty);
        }
    }
}
