using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Serilog.Core;
using Serilog.Events;

namespace Easy.Platform.Common.Logging;

public class PlatformActivityTracingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ActivitySpanId", activity?.GetSpanId()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ActivityTraceId", activity?.GetTraceId()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ActivityParentId", activity?.GetParentId()));
    }
}
