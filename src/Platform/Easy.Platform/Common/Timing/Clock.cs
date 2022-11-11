namespace Easy.Platform.Common.Timing;

/// <summary>
/// This is system clock. The Clock use Local TimeZone by default, which is Clock.Now is local time.
/// SetProvider to UtcClockProvider to use Utc Time.
/// </summary>
public static class Clock
{
    static Clock()
    {
        Provider = new LocalClockProvider();
        CurrentTimeZone = TimeZoneInfo.Local;
    }

    public static IClockProvider Provider { get; private set; }

    public static DateTime Now => Provider.Now;

    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTimeKind Kind => Provider.Kind;

    /// <summary>
    /// Current Timezone info of the clock.
    /// </summary>
    public static TimeZoneInfo CurrentTimeZone { get; private set; }

    public static DateTime Normalize(DateTime dateTime)
    {
        return Provider.Normalize(dateTime);
    }

    public static void SetProvider(IClockProvider clockProvider)
    {
        Provider = clockProvider ?? throw new ArgumentNullException(nameof(clockProvider));
    }

    public static void SetCurrentTimeZone(TimeZoneInfo timeZoneInfo)
    {
        CurrentTimeZone = timeZoneInfo ?? throw new ArgumentNullException(nameof(timeZoneInfo));
    }

    public static DateTime FromUtcToCurrentTimeZone(DateTime dateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(dateTime, CurrentTimeZone);
    }

    public static DateTime FromCurrentTimeZoneToUtc(DateTime dateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(dateTime, CurrentTimeZone);
    }
}
