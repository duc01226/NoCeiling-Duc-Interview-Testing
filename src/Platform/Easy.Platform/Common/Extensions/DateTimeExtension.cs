using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class DateTimeExtension
{
    public static DateTime FirstDateOfMonth(this DateTime dateTime)
    {
        return new DateTime(
            dateTime.Year,
            dateTime.Month,
            1,
            0,
            0,
            0,
            dateTime.Kind);
    }

    public static DateTime LastDateOfMonth(this DateTime dateTime)
    {
        return dateTime.FirstDateOfMonth().AddMonths(1).AddSeconds(-1);
    }

    public static DateTime MiddleDateOfMonth(this DateTime dateTime)
    {
        return new DateTime(
            dateTime.Year,
            dateTime.Month,
            15,
            0,
            0,
            0,
            dateTime.Kind);
    }

    public static DateTime StartOfDate(this DateTime dateTime)
    {
        return dateTime.Date;
    }

    public static DateTime EndOfDate(this DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddTicks(-1);
    }

    public static DateTime WithTimeZone(this DateTime dateTime, string timeZoneId)
    {
        var unspecifiedDateTime = dateTime.SpecifyKind(DateTimeKind.Unspecified);
        var timeZoneInfo = Util.TimeZoneParser.TryGetTimeZoneById(timeZoneId);

        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZoneInfo);

        return TimeZoneInfo.ConvertTime(utcDateTime, timeZoneInfo);
    }

    public static DateTime ConvertToUtc(this DateTime dateTime, string timeZoneId)
    {
        var unspecifiedDateTime = dateTime.SpecifyKind(DateTimeKind.Unspecified);
        var timeZoneInfo = Util.TimeZoneParser.TryGetTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZoneInfo);
    }

    public static DateTime ToDateOfMonToSunWeek(this DateTime currentDate, MonToSunDayOfWeeks monToSunDayOfWeek)
    {
        var firstDateOfMonToSunWeek = currentDate.AddDays(-(int)currentDate.MonToSunDayOfWeek());

        return firstDateOfMonToSunWeek.AddDays((int)monToSunDayOfWeek);
    }

    public static MonToSunDayOfWeeks MonToSunDayOfWeek(this DateTime currentDate)
    {
        return currentDate.DayOfWeek.Parse<MonToSunDayOfWeeks>();
    }

    public static DateTime ConvertToTimeZone(this DateTime dateTime, int timeZoneOffsetMinutes)
    {
        var hours = -timeZoneOffsetMinutes / 60;

        return dateTime.ToUniversalTime().AddHours(hours);
    }

    public static DateTime ConvertToTimeZone(this DateTime dateTime, TimeZoneInfo timeZoneInfo)
    {
        if (timeZoneInfo is null) return dateTime;

        return TimeZoneInfo.ConvertTimeFromUtc(dateTime.ToUniversalTime(), timeZoneInfo);
    }

    public static DateTime ConvertToTimeZone(this DateTime dateTime, string timeZoneId)
    {
        var timeZoneInfo = Util.TimeZoneParser.TryGetTimeZoneById(timeZoneId);

        return timeZoneInfo != null ? dateTime.ConvertToTimeZone(timeZoneInfo) : dateTime;
    }

    public static DateTimeOffset ConvertToDateTimeOffset(this DateTime dateTime, string timeZoneId)
    {
        return new DateTimeOffset(dateTime.ConvertToTimeZone(timeZoneId));
    }

    public static DateOnly ToDateOnly(this DateTime dateTime)
    {
        return DateOnly.FromDateTime(dateTime);
    }

    public static DateTime ToUtc(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime();
    }

    public static DateTime? ToUtc(this DateTime? dateTime)
    {
        return dateTime?.ToUniversalTime();
    }

    public static DateTimeOffset ToUtc(this DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime();
    }

    public static DateTimeOffset? ToUtc(this DateTimeOffset? dateTime)
    {
        return dateTime?.ToUniversalTime();
    }

    public static DateTime SpecifyKind(this DateTime dateTime, DateTimeKind kind)
    {
        return DateTime.SpecifyKind(dateTime, kind);
    }

    public static DateTime? SpecifyKind(this DateTime? dateTime, DateTimeKind kind)
    {
        return dateTime != null ? DateTime.SpecifyKind(dateTime.Value, kind) : dateTime;
    }

    public static TimeOnly TimeOnly(this DateTime dateTime)
    {
        return System.TimeOnly.FromDateTime(dateTime);
    }

    public static DateTime SetTime(this DateTime dateTime, TimeOnly time)
    {
        return dateTime
            .Date
            .AddHours(time.Hour)
            .AddMinutes(time.Minute)
            .AddSeconds(time.Second)
            .AddMicroseconds(time.Microsecond);
    }

    public static DateTime SetTime(this DateTime dateTime, TimeOnly? time)
    {
        return time.HasValue ? dateTime.SetTime(time.Value) : dateTime;
    }

    public static DateTime TrimSeconds(this DateTime dateTime)
    {
        var time = new TimeOnly(dateTime.Hour, dateTime.Minute);
        return dateTime.SetTime(time);
    }

    public static string FormatText(this TimeOnly? timeOnly)
    {
        return timeOnly == null ? string.Empty : timeOnly.Value.ToString("HH:mm");
    }

    public enum MonToSunDayOfWeeks
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday
    }
}
