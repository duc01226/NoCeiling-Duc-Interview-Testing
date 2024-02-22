using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Timing;

/// <summary>
/// Provides a system clock. The <see cref="Clock" /> uses UTC TimeZone by default, where <see cref="Now" /> represents the current UTC time.
/// To use the local time, set the provider to <see cref="LocalClockProvider" />.
/// </summary>
public static class Clock
{
    /// <summary>
    /// Initializes the <see cref="Clock" /> class. Uses UTC clock provider by default.
    /// </summary>
    static Clock()
    {
        UseUtcProvider();
    }

    /// <summary>
    /// Gets or sets the current clock provider.
    /// </summary>
    public static IClockProvider Provider { get; private set; }

    /// <summary>
    /// Gets the current local time.
    /// </summary>
    public static DateTime Now => Provider.Now;

    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    public static DateTime UtcNow => Provider.UtcNow;

    /// <summary>
    /// Gets the current local time.
    /// </summary>
    public static DateTime LocalNow => Provider.LocalNow;

    /// <summary>
    /// Gets the <see cref="DateTimeKind" /> of the clock.
    /// </summary>
    public static DateTimeKind Kind => Provider.Kind;

    /// <summary>
    /// Gets or sets the current timezone information of the clock.
    /// </summary>
    public static TimeZoneInfo CurrentTimeZone { get; private set; }

    /// <summary>
    /// Normalizes the provided <see cref="DateTime" /> based on the clock's provider.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime" /> to normalize.</param>
    /// <returns>The normalized <see cref="DateTime" />.</returns>
    public static DateTime Normalize(DateTime dateTime)
    {
        return Provider.Normalize(dateTime);
    }

    /// <summary>
    /// Sets the clock provider.
    /// </summary>
    /// <param name="clockProvider">The clock provider to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clockProvider" /> is null.</exception>
    public static void SetProvider(IClockProvider clockProvider)
    {
        Provider = clockProvider ?? throw new ArgumentNullException(nameof(clockProvider));
    }

    /// <summary>
    /// Sets the clock provider to use the local time.
    /// </summary>
    public static void UseLocalProvider()
    {
        SetProvider(new LocalClockProvider());
        CurrentTimeZone = TimeZoneInfo.Local;
    }

    /// <summary>
    /// Sets the clock provider to use UTC time.
    /// </summary>
    public static void UseUtcProvider()
    {
        SetProvider(new UtcClockProvider());
        CurrentTimeZone = TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Sets the current timezone information.
    /// </summary>
    /// <param name="timeZoneInfo">The timezone information to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeZoneInfo" /> is null.</exception>
    public static void SetCurrentTimeZone(TimeZoneInfo timeZoneInfo)
    {
        CurrentTimeZone = timeZoneInfo ?? throw new ArgumentNullException(nameof(timeZoneInfo));
    }

    public static DateTime NewDate(int year, int month, int day)
    {
        return NewDate(year, month, day, null);
    }

    public static DateTime NewDate(int year, int month, int day, DateTimeKind? kind)
    {
        return NewDate(year, month, day, 0, 0, 0, kind);
    }

    public static DateTime NewDate(int year, int month, int day, int hour, int minute = 0, int second = 0, DateTimeKind? kind = null)
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
