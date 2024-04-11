using System.Globalization;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class DateTimeExtension
{
    public const double DaysPerYear = 365.25;

    /// <summary>
    /// Returns the first date of the month for the specified date.
    /// </summary>
    /// <param name="dateTime">The DateTime instance.</param>
    /// <returns>The first date of the month.</returns>
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

    /// <summary>
    /// Returns the last date of the month for the specified date.
    /// </summary>
    /// <param name="dateTime">The DateTime instance.</param>
    /// <returns>The last date of the month.</returns>
    public static DateTime LastDateOfMonth(this DateTime dateTime)
    {
        return dateTime.FirstDateOfMonth().AddMonths(1).AddSeconds(-1);
    }

    /// <summary>
    /// Returns the middle date of the month for the specified date.
    /// </summary>
    /// <param name="dateTime">The DateTime instance.</param>
    /// <returns>The middle date of the month.</returns>
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

    /// <summary>
    /// Returns the start of the date for the specified date.
    /// </summary>
    /// <param name="dateTime">The DateTime instance.</param>
    /// <returns>The start of the date.</returns>
    public static DateTime StartOfDate(this DateTime dateTime)
    {
        return dateTime.Date;
    }

    /// <summary>
    /// Returns the end of the date for the specified date.
    /// </summary>
    /// <param name="dateTime">The DateTime instance.</param>
    /// <returns>The end of the date.</returns>
    public static DateTime EndOfDate(this DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddTicks(-1);
    }

    public static DateTime EndDateOfYear(this DateTime dateTime)
    {
        var startOfNextYear = new DateTime(dateTime.Year + 1, 1, 1);
        var endOfYearDate = startOfNextYear.AddDays(-1);

        return endOfYearDate;
    }

    public static double CalculateTotalMonths(this DateTime starDate, DateTime endDate)
    {
        if (starDate > endDate)
            throw new ArgumentOutOfRangeException(
                nameof(starDate),
                "The start date must be before the end date.");
        var totalDays = (endDate - starDate).TotalDays;

        var averageDaysPerMonth = DaysPerYear / 12;
        return totalDays / averageDaysPerMonth;
    }

    /// <summary>
    /// Converts the specified DateTime value to the equivalent date and time in a specified time zone.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="timeZoneId">The time zone identifier.</param>
    /// <returns>The date and time in the specified time zone.</returns>
    public static DateTime WithTimeZone(this DateTime dateTime, string timeZoneId)
    {
        var unspecifiedDateTime = dateTime.SpecifyKind(DateTimeKind.Unspecified);
        var timeZoneInfo = Util.TimeZoneParser.TryGetTimeZoneById(timeZoneId);

        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZoneInfo);

        return TimeZoneInfo.ConvertTime(utcDateTime, timeZoneInfo);
    }

    /// <summary>
    /// Converts the specified DateTime value to the equivalent UTC date and time using a specified time zone.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="timeZoneId">The time zone identifier.</param>
    /// <returns>The UTC date and time.</returns>
    public static DateTime ConvertToUtc(this DateTime dateTime, string timeZoneId)
    {
        var unspecifiedDateTime = dateTime.SpecifyKind(DateTimeKind.Unspecified);
        var timeZoneInfo = Util.TimeZoneParser.TryGetTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZoneInfo);
    }

    /// <summary>
    /// Returns the date of the specified day of the week in the same week as the specified date.
    /// </summary>
    /// <param name="currentDate">The DateTime instance.</param>
    /// <param name="monToSunDayOfWeek">The day of the week.</param>
    /// <returns>The date of the specified day of the week in the same week as the specified date.</returns>
    public static DateTime ToDateOfMonToSunWeek(this DateTime currentDate, MonToSunDayOfWeeks monToSunDayOfWeek)
    {
        var firstDateOfMonToSunWeek = currentDate.AddDays(-(int)currentDate.MonToSunDayOfWeek());

        return firstDateOfMonToSunWeek.AddDays((int)monToSunDayOfWeek);
    }

    /// <summary>
    /// Returns the day of the week for the specified date.
    /// </summary>
    /// <param name="currentDate">The DateTime instance.</param>
    /// <returns>The day of the week.</returns>
    public static MonToSunDayOfWeeks MonToSunDayOfWeek(this DateTime currentDate)
    {
        return currentDate.DayOfWeek.Parse<MonToSunDayOfWeeks>();
    }

    /// <summary>
    /// Converts the specified DateTime value to the equivalent date and time in a specified time zone offset.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="timeZoneOffsetMinutes">The time zone offset in minutes.</param>
    /// <returns>The date and time in the specified time zone offset.</returns>
    public static DateTime ConvertToTimeZone(this DateTime dateTime, int timeZoneOffsetMinutes)
    {
        var hours = -timeZoneOffsetMinutes / 60;

        return dateTime.ToUniversalTime().AddHours(hours);
    }

    /// <summary>
    /// Converts the specified DateTime value to the equivalent date and time in a specified time zone.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="timeZoneInfo">The TimeZoneInfo instance.</param>
    /// <returns>The date and time in the specified time zone.</returns>
    public static DateTime ConvertToTimeZone(this DateTime dateTime, TimeZoneInfo timeZoneInfo)
    {
        if (timeZoneInfo is null) return dateTime;

        return TimeZoneInfo.ConvertTimeFromUtc(dateTime.ToUniversalTime(), timeZoneInfo);
    }

    /// <summary>
    /// Converts the specified DateTime value to the equivalent date and time in a specified time zone.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="timeZoneId">The time zone identifier.</param>
    /// <returns>The date and time in the specified time zone.</returns>
    public static DateTime ConvertToTimeZone(this DateTime dateTime, string timeZoneId)
    {
        var timeZoneInfo = Util.TimeZoneParser.TryGetTimeZoneById(timeZoneId);

        return timeZoneInfo != null ? dateTime.ConvertToTimeZone(timeZoneInfo) : dateTime;
    }

    /// <summary>
    /// Converts the specified DateTime value to a DateTimeOffset value in a specified time zone.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="timeZoneId">The time zone identifier.</param>
    /// <returns>The DateTimeOffset value in the specified time zone.</returns>
    public static DateTimeOffset ConvertToDateTimeOffset(this DateTime dateTime, string timeZoneId)
    {
        return new DateTimeOffset(dateTime.ConvertToTimeZone(timeZoneId));
    }

    /// <summary>
    /// Converts the specified DateTime value to a DateOnly value.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <returns>The DateOnly value.</returns>
    public static DateOnly ToDateOnly(this DateTime dateTime)
    {
        return DateOnly.FromDateTime(dateTime);
    }

    /// <summary>
    /// Converts the specified DateTime object to Coordinated Universal Time (UTC).
    /// </summary>
    /// <param name="dateTime">The DateTime object to be converted.</param>
    /// <returns>The DateTime object converted to UTC.</returns>
    public static DateTime ToUtc(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime();
    }

    /// <summary>
    /// Converts the specified nullable DateTime to UTC.
    /// </summary>
    /// <param name="dateTime">The nullable DateTime to convert.</param>
    /// <returns>The nullable DateTime in UTC, or null if the input is null.</returns>
    public static DateTime? ToUtc(this DateTime? dateTime)
    {
        return dateTime?.ToUniversalTime();
    }

    /// <summary>
    /// Converts the specified DateTimeOffset object to Coordinated Universal Time (UTC).
    /// </summary>
    /// <param name="dateTime">The DateTimeOffset object to be converted.</param>
    /// <returns>The DateTimeOffset object converted to UTC.</returns>
    public static DateTimeOffset ToUtc(this DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime();
    }

    /// <summary>
    /// Converts the specified DateTimeOffset? value to the equivalent date and time in Coordinated Universal Time (UTC).
    /// </summary>
    /// <param name="dateTime">The DateTimeOffset? value to convert.</param>
    /// <returns>The date and time in UTC. If the input is null, returns null.</returns>
    public static DateTimeOffset? ToUtc(this DateTimeOffset? dateTime)
    {
        return dateTime?.ToUniversalTime();
    }

    /// <summary>
    /// Specifies the kind (local, UTC, unspecified) of the provided DateTime object.
    /// </summary>
    /// <param name="dateTime">The DateTime object to specify the kind for.</param>
    /// <param name="kind">The DateTimeKind value to assign to the DateTime object.</param>
    /// <returns>A new DateTime object that has the same number of ticks as the object represented by the dateTime parameter, but is designated as either local time, Coordinated Universal Time (UTC), or neither, as indicated by the kind parameter.</returns>
    public static DateTime SpecifyKind(this DateTime dateTime, DateTimeKind kind)
    {
        return DateTime.SpecifyKind(dateTime, kind);
    }

    /// <summary>
    /// Sets the DateTimeKind for the specified DateTime.
    /// </summary>
    /// <param name="dateTime">The DateTime to set the DateTimeKind for.</param>
    /// <param name="kind">The DateTimeKind to set.</param>
    /// <returns>The DateTime with the specified DateTimeKind.</returns>
    public static DateTime? SpecifyKind(this DateTime? dateTime, DateTimeKind kind)
    {
        return dateTime != null ? DateTime.SpecifyKind(dateTime.Value, kind) : dateTime;
    }

    /// <summary>
    /// Sets the DateTimeKind for the specified nullable DateTime.
    /// </summary>
    /// <param name="dateTime">The nullable DateTime to set the DateTimeKind for.</param>
    /// <param name="kind">The DateTimeKind to set.</param>
    /// <returns>The nullable DateTime with the specified DateTimeKind, or null if the input is null.</returns>
    public static TimeOnly TimeOnly(this DateTime dateTime)
    {
        return System.TimeOnly.FromDateTime(dateTime);
    }

    /// <summary>
    /// Extracts the time from the specified DateTime as a TimeOnly instance.
    /// </summary>
    /// <param name="dateTime">The DateTime to extract the time from.</param>
    /// <returns>The time as a TimeOnly instance.</returns>
    public static DateTime SetTime(this DateTime dateTime, TimeOnly time)
    {
        return dateTime
            .Date
            .AddHours(time.Hour)
            .AddMinutes(time.Minute)
            .AddSeconds(time.Second)
            .AddMicroseconds(time.Microsecond);
    }

    /// <summary>
    /// Sets the time of the specified DateTime using a TimeOnly instance.
    /// </summary>
    /// <param name="dateTime">The DateTime to set the time for.</param>
    /// <param name="time">The TimeOnly instance to use.</param>
    /// <returns>The DateTime with the specified time.</returns>
    public static DateTime SetTime(this DateTime dateTime, TimeOnly? time)
    {
        return time.HasValue ? dateTime.SetTime(time.Value) : dateTime;
    }

    /// <summary>
    /// Trims the seconds and milliseconds from the specified DateTime.
    /// </summary>
    /// <param name="dateTime">The DateTime to trim the seconds and milliseconds from.</param>
    /// <returns>The DateTime without seconds and milliseconds.</returns>
    public static DateTime TrimSeconds(this DateTime dateTime)
    {
        var time = new TimeOnly(dateTime.Hour, dateTime.Minute);
        return dateTime.SetTime(time);
    }

    /// <summary>
    /// Trims the milliseconds from the specified DateTime.
    /// </summary>
    /// <param name="dateTime">The DateTime to trim the milliseconds from.</param>
    /// <returns>The DateTime without milliseconds.</returns>
    public static DateTime TrimMilliseconds(this DateTime dateTime)
    {
        var time = new TimeOnly(dateTime.Hour, dateTime.Minute, dateTime.Second);
        return dateTime.SetTime(time);
    }

    /// <summary>
    /// Return the next occurrence of a specified day of the week after the specified number of weeks
    /// Ex: currentDate = new DateTime(2022, 1, 1)
    /// Next Thursday after 2 weeks: currentDate.GetNextDayOfWeeks(DayOfWeek.Thursday, 2)
    /// </summary>
    public static DateTime GetNextDateWithDayOfWeek(this DateTime dateTime, DayOfWeek dayOfWeek, int numberOfWeeks)
    {
        var daysUntilNextDay = ((int)dayOfWeek - (int)dateTime.DayOfWeek + 7) % 7;
        var nextDay = dateTime.AddDays(daysUntilNextDay);

        if (AreDatesInSameWeek(dateTime, nextDay))
            nextDay = nextDay.AddDays(7 * numberOfWeeks);

        return nextDay;
    }

    public static bool AreDatesInSameWeek(this DateTime date1, DateTime date2)
    {
        return GetIso8601WeekOfYear(date1) == GetIso8601WeekOfYear(date2) &&
               date1.Year == date2.Year;
    }

    public static bool AreDatesInSameYearAndMonth(this DateTime date1, DateTime date2)
    {
        return date1.Year == date2.Year &&
               date1.Month == date2.Month;
    }

    /// <summary>
    /// Checks whether two DateTime instances represent the same date,
    /// Considering the end of the month as a special case.
    /// </summary>
    public static bool IsSameDateConsideringMonthEnd(this DateTime date1, DateTime date2)
    {
        if (IsEndOfMonth(date1) && IsEndOfMonth(date2))
            return true;

        return date1.Date == date2.Date;
    }

    public static bool IsEndOfMonth(this DateTime date)
    {
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

        return date.Day == daysInMonth;
    }

    /// <summary>
    /// Check given date is within the specified number of future weeks
    /// </summary>
    public static bool IsDateWithinWeeksFromNow(this DateTime date, int numberOfWeeks = 1)
    {
        var nextWeekStart = DateTime.Now.AddDays(7 - (int)DateTime.Now.DayOfWeek + 1);
        if (numberOfWeeks > 1)
            nextWeekStart = nextWeekStart.AddDays(7 * (numberOfWeeks - 1));
        var nextWeekEnd = nextWeekStart.AddDays(6);

        return nextWeekStart.StartOfDate() <= date && date <= nextWeekEnd.EndOfDate();
    }

    /// <summary>
    /// Check given date is within the specified number of future months
    /// </summary>
    public static bool IsDateWithinMonthsFromNow(this DateTime date, int numberOfMonths)
    {
        var firstDayOfNextMonth = new DateTime(date.Year, date.Month, 1).AddMonths(numberOfMonths);

        return firstDayOfNextMonth.Date <= date.Date && date.Date < firstDayOfNextMonth.AddMonths(1).Date;
    }

    /// <summary>
    /// Return the ISO8601 week number of the year for a specific date
    /// </summary>
    public static int GetIso8601WeekOfYear(DateTime date)
    {
        var dfi = DateTimeFormatInfo.CurrentInfo;
        var calendar = dfi.Calendar;

        // Set Monday as the first day of the week
        var rule = dfi.FirstDayOfWeek;
        var jan1 = new DateTime(date.Year, 1, 1);

        // Count the number of days from the first day of the year to the target date
        var days = (int)date.Subtract(jan1).TotalDays;
        var week = ((days + (int)rule - 1) / 7) + 1;

        if (week == 1 && date.Month == 12)
        {
            var daysUntilEndOfWeek = (int)dfi.FirstDayOfWeek - (int)date.DayOfWeek;
            if (daysUntilEndOfWeek < 0) daysUntilEndOfWeek += 7;

            if (date.AddDays(daysUntilEndOfWeek).Year > date.Year)
                week = calendar.GetWeekOfYear(date.AddDays(daysUntilEndOfWeek), dfi.CalendarWeekRule, dfi.FirstDayOfWeek);
        }

        return week;
    }

    public static DayOfWeek ToDayOfWeek(this MonToSunDayOfWeeks dayOfWeek)
    {
        return dayOfWeek switch
        {
            MonToSunDayOfWeeks.Monday => DayOfWeek.Monday,
            MonToSunDayOfWeeks.Tuesday => DayOfWeek.Tuesday,
            MonToSunDayOfWeeks.Wednesday => DayOfWeek.Wednesday,
            MonToSunDayOfWeeks.Thursday => DayOfWeek.Thursday,
            MonToSunDayOfWeeks.Friday => DayOfWeek.Friday,
            MonToSunDayOfWeeks.Saturday => DayOfWeek.Saturday,
            MonToSunDayOfWeeks.Sunday => DayOfWeek.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, null)
        };
    }

    public static string ToMonthlyString(this int month)
    {
        return month switch
        {
            1 => "January",
            2 => "February",
            3 => "March",
            4 => "April",
            5 => "May",
            6 => "June",
            7 => "July",
            8 => "August",
            9 => "September",
            10 => "October",
            11 => "November",
            12 => "December",
            _ => throw new ArgumentOutOfRangeException(nameof(month), month, "Invalid month value. Month value should be between 1 and 12.")
        };
    }

    public static string ToDayWithOrdinalSuffixString(this int day)
    {
        // Check if the day value is outside the valid range of 1 to 31.
        if (day > 31 || day < 1) throw new ArgumentOutOfRangeException(nameof(day), day, "Invalid day value. Day value should be between 1 and 31.");

        // Check if the day value is between day 4 and 20. In this range, the ordinal suffix is always "th".
        if (day > 3 && day < 21) return day + "th";

        // For all other cases, determine the ordinal suffix based on the last digit of the day value.
        return (day % 10) switch
        {
            1 => $"{day}st",
            2 => $"{day}nd",
            3 => $"{day}rd",
            _ => $"{day}th"
        };
    }

    public static int DaysInYear(int year)
    {
        return DateTime.IsLeapYear(year) ? 366 : 365;
    }

    /// <summary>
    /// Represents the days of the week, starting from Monday and ending on Sunday.
    /// </summary>
    /// <remarks>
    /// This enumeration is used in various parts of the application where the week is considered to start on Monday instead of Sunday.
    /// </remarks>
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
