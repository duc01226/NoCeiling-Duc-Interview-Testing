using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Timing;

/// <summary>
/// This is system clock. The Clock use Utc TimeZone by default, which is Clock.Now is Utc time.
/// SetProvider to LocalClockProvider to use Local Time.
/// </summary>
public static class Clock
{
    static Clock()
    {
        UseUtcProvider();
    }

    public static IClockProvider Provider { get; private set; }

    public static DateTime Now => Provider.Now;

    public static DateTime UtcNow => Provider.UtcNow;

    public static DateTime LocalNow => Provider.LocalNow;

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

    public static void UseLocalProvider()
    {
        SetProvider(new LocalClockProvider());
        CurrentTimeZone = TimeZoneInfo.Local;
    }

    public static void UseUtcProvider()
    {
        SetProvider(new UtcClockProvider());
        CurrentTimeZone = TimeZoneInfo.Utc;
    }

    public static void SetCurrentTimeZone(TimeZoneInfo timeZoneInfo)
    {
        CurrentTimeZone = timeZoneInfo ?? throw new ArgumentNullException(nameof(timeZoneInfo));
    }

    public static DateTime NewDate(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, DateTimeKind? kind = null)
    {
        return new DateTime(year, month, day, hour, minute, second).SpecifyKind(kind ?? Kind);
    }

    public static DateTime NewUtcDate(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTime(year, month, day, hour, minute, second).SpecifyKind(DateTimeKind.Utc);
    }

    public static DateTime NewLocalDate(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTime(year, month, day, hour, minute, second).SpecifyKind(DateTimeKind.Local);
    }

    public static DateTime EndOfMonth(int year, int month)
    {
        return NewDate(year, month, 1).AddMonths(1).AddDays(-1).EndOfDate();
    }

    public static DateTime EndOfMonth(DateTime date)
    {
        return NewDate(date.Year, date.Month, 1).AddMonths(1).AddDays(-1).EndOfDate();
    }

    public static DateTime StartOfMonth(int year, int month)
    {
        return NewDate(year, month, 1);
    }

    public static DateTime StartOfMonth(DateTime date)
    {
        return NewDate(date.Year, date.Month, 1);
    }

    public static DateTime EndOfCurrentMonth()
    {
        return EndOfMonth(Now.Year, Now.Month);
    }

    public static DateTime StartOfCurrentMonth()
    {
        return StartOfMonth(Now.Year, Now.Month);
    }

    public static DateTime EndOfLastMonth()
    {
        return EndOfCurrentMonth().AddMonths(-1);
    }

    public static DateTime StartOfLastMonth()
    {
        return StartOfCurrentMonth().AddMonths(-1);
    }

    public static DateTime EndOfNextMonth()
    {
        return EndOfCurrentMonth().AddMonths(1);
    }

    public static DateTime StartOfNextMonth()
    {
        return StartOfCurrentMonth().AddMonths(1);
    }

    public static DateTime DayOfCurrentMonth(int day)
    {
        return NewDate(Now.Year, Now.Month, day);
    }

    public static DateTime DayOfNextMonth(int day)
    {
        return NewDate(Now.Year, Now.Month, day).AddMonths(1);
    }

    public static DateTime DayOfLastMonth(int day)
    {
        return NewDate(Now.Year, Now.Month, day).AddMonths(-1);
    }
}
