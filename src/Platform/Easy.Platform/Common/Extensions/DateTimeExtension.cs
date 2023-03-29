using TimeZoneConverter;

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

    public static DateTime EndOfDate(this DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddSeconds(-1);
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

    public static DateTime ConvertToTimeZone(this DateTime dateTime, int timeZoneOffset)
    {
        var hours = -timeZoneOffset / 60;
        return dateTime.ToUniversalTime().AddHours(hours);
    }

    public static DateTime ConvertToTimeZone(this DateTime dateTime, TimeZoneInfo timeZoneInfo)
    {
        if (timeZoneInfo is null)
            return dateTime;

        return TimeZoneInfo.ConvertTimeFromUtc(dateTime.ToUniversalTime(), timeZoneInfo);
    }

    public static DateTime ConvertToTimeZone(this DateTime dateTime, string ianaTimeZone)
    {
        if (ianaTimeZone is null || !TZConvert.TryGetTimeZoneInfo(ianaTimeZone, out var timeZoneInfo))
            return dateTime;

        return dateTime.ConvertToTimeZone(timeZoneInfo);
    }

    public static DateTimeOffset ConvertToDateTimeOffset(this DateTime dateTime, string ianaTimeZone)
    {
        return new DateTimeOffset(dateTime.ConvertToTimeZone(ianaTimeZone));
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
