namespace Easy.Platform.Common.Extensions;

public static class ToTimeSpanExtension
{
    public static TimeSpan Milliseconds(this int value)
    {
        return TimeSpan.FromMilliseconds(value);
    }

    public static TimeSpan Seconds(this int value)
    {
        return TimeSpan.FromSeconds(value);
    }

    public static TimeSpan Minutes(this int value)
    {
        return TimeSpan.FromMinutes(value);
    }

    public static TimeSpan Hours(this int value)
    {
        return TimeSpan.FromHours(value);
    }

    public static TimeSpan Days(this int value)
    {
        return TimeSpan.FromDays(value);
    }

    public static TimeSpan Years(this int value)
    {
        return TimeSpan.FromDays(value * 365);
    }

    public static TimeSpan Milliseconds(this long value)
    {
        return TimeSpan.FromMilliseconds(value);
    }

    public static TimeSpan Seconds(this long value)
    {
        return TimeSpan.FromSeconds(value);
    }

    public static TimeSpan Minutes(this long value)
    {
        return TimeSpan.FromMinutes(value);
    }

    public static TimeSpan Hours(this long value)
    {
        return TimeSpan.FromHours(value);
    }

    public static TimeSpan Days(this long value)
    {
        return TimeSpan.FromDays(value);
    }

    public static TimeSpan Years(this long value)
    {
        return TimeSpan.FromDays(value * 365);
    }

    public static TimeSpan Milliseconds(this double value)
    {
        return TimeSpan.FromMilliseconds(value);
    }

    public static TimeSpan Seconds(this double value)
    {
        return TimeSpan.FromSeconds(value);
    }

    public static TimeSpan Minutes(this double value)
    {
        return TimeSpan.FromMinutes(value);
    }

    public static TimeSpan Hours(this double value)
    {
        return TimeSpan.FromHours(value);
    }

    public static TimeSpan Days(this double value)
    {
        return TimeSpan.FromDays(value);
    }

    public static TimeSpan Years(this double value)
    {
        return TimeSpan.FromDays(value * 365);
    }
}
