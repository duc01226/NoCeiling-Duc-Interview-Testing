#nullable enable
namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformMessageBusConfig
{
    /// <summary>
    /// Config the time to true to log consumer process time
    /// </summary>
    bool EnableLogConsumerProcessTime { get; set; }

    /// <summary>
    /// Config the time in milliseconds to log warning if the process consumer time is over LogConsumerProcessWarningTimeMilliseconds.
    /// </summary>
    long LogSlowProcessWarningTimeMilliseconds { get; set; }
}

public class PlatformMessageBusConfig
    : IPlatformMessageBusConfig
{
    public const long DefaultProcessWarningTimeMilliseconds = 10000;

    /// <summary>
    /// Config the time to true to log consumer process time
    /// </summary>
    public bool EnableLogConsumerProcessTime { get; set; }

    /// <summary>
    /// Config the time in milliseconds to log warning if the process consumer time is over LogConsumerProcessWarningTimeMilliseconds.
    /// </summary>
    public long LogSlowProcessWarningTimeMilliseconds { get; set; } = DefaultProcessWarningTimeMilliseconds;
}
