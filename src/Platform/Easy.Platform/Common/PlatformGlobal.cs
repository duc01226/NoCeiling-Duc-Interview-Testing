using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common;

public abstract class PlatformGlobal
{
    /// <summary>
    /// This by default will be set as root service provider of the application on application module init
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; }

    public static ILoggerFactory LoggerFactory => ServiceProvider.GetRequiredService<ILoggerFactory>();

    public static IConfiguration Configuration => ServiceProvider.GetRequiredService<IConfiguration>();

    public static ILogger CreateDefaultLogger()
    {
        return CreateDefaultLogger(ServiceProvider);
    }

    public static ILogger CreateDefaultLogger(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Easy.Platform");
    }

    public static void SetRootServiceProvider(IServiceProvider rootServiceProvider)
    {
        ServiceProvider = rootServiceProvider;
    }

    public static class MemoryCollector
    {
        private static readonly Util.TaskRunner.Throttler CollectGarbageMemoryThrottler = new();
        public static int DefaultCollectGarbageMemoryThrottleMilliseconds { get; set; } = 5000;

        public static void CollectGarbageMemory(int? throttleSeconds = null)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CollectGarbageMemoryThrottler.ThrottleExecuteAsync(
                () => Task.Run(
                    () =>
                    {
                        GC.Collect();
                    }),
                TimeSpan.FromMilliseconds(throttleSeconds ?? DefaultCollectGarbageMemoryThrottleMilliseconds));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public static void CollectGarbageMemory(int generation, GCCollectionMode mode, bool blocking, bool compacting, int? throttleSeconds = null)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CollectGarbageMemoryThrottler.ThrottleExecuteAsync(
                () => Task.Run(
                    () =>
                    {
                        GC.Collect(generation, mode, blocking, compacting);
                    }),
                TimeSpan.FromMilliseconds(throttleSeconds ?? DefaultCollectGarbageMemoryThrottleMilliseconds));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
