using System.Reflection;

namespace Easy.Platform.Application.Context;

public interface IPlatformApplicationSettingContext
{
    public string ApplicationName { get; }

    public Assembly ApplicationAssembly { get; }
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
}
