using System.Reflection;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Application;

public interface IPlatformApplicationSettingContext
{
    public static HashSet<string> DefaultIgnoreRequestContextKeys { get; } = ["Authorization"];

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

    /// <summary>
    /// Used to config which request context keys will be ignored in logs, events and bus messages. Default value is null, then it will fall back to use <see cref="DefaultIgnoreRequestContextKeys" />
    /// </summary>
    public HashSet<string>? IgnoreRequestContextKeys { get; set; }

    /// <summary>
    /// When true, system will log more detailed information log so that we can debug easier
    /// </summary>
    public bool IsDebugInformationMode { get; set; }

    /// <summary>
    /// Return <see cref="IgnoreRequestContextKeys" /> or <see cref="DefaultIgnoreRequestContextKeys" /> if <see cref="IgnoreRequestContextKeys" /> is null
    /// </summary>
    public HashSet<string> GetIgnoreRequestContextKeys()
    {
        return IgnoreRequestContextKeys ?? DefaultIgnoreRequestContextKeys;
    }

    public void ProcessAutoGarbageCollect()
    {
        if (AutoGarbageCollectPerProcessRequestOrBusMessage)
            Util.GarbageCollector.Collect(AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds);
    }
}

public class PlatformApplicationSettingContext : IPlatformApplicationSettingContext
{
    public const string DefaultIsDebugInformationModeConfigurationKey = "IsDebugInformationMode";

    private Assembly applicationAssembly;
    private string applicationName;

    public PlatformApplicationSettingContext(IServiceProvider serviceProvider)
    {
        IsDebugInformationMode = serviceProvider.GetRequiredService<IConfiguration>().GetValue<bool?>(DefaultIsDebugInformationModeConfigurationKey) == true;
    }

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

    public bool AutoGarbageCollectPerProcessRequestOrBusMessage { get; set; }

    /// <summary>
    /// <inheritdoc cref="IPlatformApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds" /> <br />
    /// Default value is <see cref="Util.GarbageCollector.DefaultCollectGarbageMemoryThrottleSeconds" />.
    /// </summary>
    public double AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds { get; set; } = Util.GarbageCollector.DefaultCollectGarbageMemoryThrottleSeconds;

    public HashSet<string>? IgnoreRequestContextKeys { get; set; } = IPlatformApplicationSettingContext.DefaultIgnoreRequestContextKeys;

    public bool IsDebugInformationMode { get; set; }
}
