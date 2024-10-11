using System.Diagnostics;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.Caching;

public class PlatformCacheSettings
{
    public PlatformCacheEntryOptions DefaultCacheEntryOptions { get; set; } = new();

    public PlatformCacheSettingsSlowWarningConfig SlowWarning { get; set; } = new();

    public async Task<T> ExecuteWithSlowWarning<T>(Func<Task<T>> executeFn, Func<ILogger> loggerFactory, bool forSetData = false)
    {
        if (SlowWarning.IsEnabled)
        {
            // Store stack trace before call redisCache.Value.GetAsync to keep the original stack trace to log
            // after redisCache.Value.GetAsync will lose full stack trace (may because it connects async to other external service)
            var fullStackTrace = PlatformEnvironment.StackTrace();

            var startQueryTimeStamp = Stopwatch.GetTimestamp();

            var result = await executeFn();

            var queryElapsedTime = Stopwatch.GetElapsedTime(startQueryTimeStamp);

            if (queryElapsedTime.TotalMilliseconds >= SlowWarning.GetSlowQueryMillisecondsThreshold(forSetData))
                loggerFactory()
                    .Log(
                        SlowWarning.IsLogWarningAsError ? LogLevel.Error : LogLevel.Warning,
                        "[SlowDistributedCacheWarning][IsLogWarningAsError:{IsLogWarningAsError}][ForWrite:{ForWrite}] Slow redis cache execution. QueryElapsedTime.TotalMilliseconds:{QueryElapsedTime}. " +
                        "SlowCacheWarningTrackTrace:{TrackTrace}",
                        SlowWarning.IsLogWarningAsError,
                        forSetData.ToString(),
                        queryElapsedTime.TotalMilliseconds,
                        fullStackTrace);

            return result;
        }

        return await executeFn();
    }

    public async Task ExecuteWithSlowWarning(Func<Task> executeFn, Func<ILogger> loggerFactory, bool forSetData = false)
    {
        await ExecuteWithSlowWarning(executeFn.ToAsyncFunc(), loggerFactory, forSetData);
    }
}

public class PlatformCacheSettingsSlowWarningConfig
{
    public bool IsEnabled { get; set; }

    /// <summary>
    /// If true, the warning log will be logged as Error level message
    /// </summary>
    public bool IsLogWarningAsError { get; set; }

    public int SlowGetMillisecondsThreshold { get; set; } = 50;

    public int SlowWriteQueryMillisecondsThreshold { get; set; } = 100;

    public int GetSlowQueryMillisecondsThreshold(bool forSetData)
    {
        return forSetData ? SlowWriteQueryMillisecondsThreshold : SlowGetMillisecondsThreshold;
    }
}
