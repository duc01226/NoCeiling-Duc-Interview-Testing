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
        return dateTime.ToUniversalTime().AddHours(-timeZoneOffset / 60);
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
