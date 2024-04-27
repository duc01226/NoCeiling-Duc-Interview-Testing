using System.Reflection;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Application;

public interface IPlatformApplicationSettingContext
{
    public string ApplicationName { get; }

    public Assembly ApplicationAssembly { get; }

    /// <summary>
    /// If true, garbage collector will run every time having a cqrs request or bus message consumer running
    /// </summary>
    public bool AutoGarbageCollectPerProcessRequestOrBusMessage { get; set; }

    /// <summary>
    /// Throttle time seconds to run garbage collect when <see cref="AutoGarbageCollectPerProcessRequestOrBusMessage" /> is true. Example if value is 5, mean that
    /// maximum is 1 collect run per 5 seconds
    /// </summary>
    public double AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds { get; set; }
}

public class PlatformApplicationSettingContext : IPlatformApplicationSettingContext
{
    private Assembly applicationAssembly;
    private string applicationName;

    public string ApplicationName
    {
        get => applicationName ?? GetType().Assembly.GetName().Name;
        set => applicationName = value;
    }

    public Assembly ApplicationAssembly
    {
        get => applicationAssembly ?? GetType().Assembly;
        set => applicationAssembly = value;
    }

    public bool AutoGarbageCollectPerProcessRequestOrBusMessage { get; set; } = true;

    /// <summary>
    /// <inheritdoc cref="IPlatformApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds" /> <br />
    /// Default value is <see cref="Util.GarbageCollector.DefaultCollectGarbageMemoryThrottleSeconds" />.
    /// </summary>
    public double AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds { get; set; } = Util.GarbageCollector.DefaultCollectGarbageMemoryThrottleSeconds;
}
