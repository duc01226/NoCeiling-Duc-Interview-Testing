using System.Reflection;

namespace Easy.Platform.Application;

public interface IPlatformApplicationSettingContext
{
    public string ApplicationName { get; }

    public Assembly ApplicationAssembly { get; }

    public bool AutoGarbageCollectPerProcessRequestOrBusMessage { get; set; }

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

    public double AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds { get; set; } = 2;
}
