using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DateRangeBuilder
    {
        /// <summary>
        /// Build an list DateTime from startDate to endDate
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="ignoreDayOfWeeks"></param>
        /// <returns>List of DateTime or Empty List</returns>
        public static List<DateTime> BuildDateRange(DateTime startDate, DateTime endDate, HashSet<DayOfWeek> ignoreDayOfWeeks = null)
        {
            if (startDate.Date > endDate.Date) return [];

            return Enumerable.Range(0, endDate.Date.Subtract(startDate.Date).Days + 1)
                .Select(offset => startDate.AddDays(offset))
                .WhereIf(ignoreDayOfWeeks != null, date => !ignoreDayOfWeeks.Contains(date.DayOfWeek))
                .ToList()
                .PipeIf(
                    p => p.Any(),
                    p =>
                    {
                        p[p.Count - 1] = p[p.Count - 1].SetTime(endDate.TimeOnly());
                        return p;
                    });
        }

        public static List<DateOnly> BuildDateRangeDateOnly(DateTime startDate, DateTime endDate, HashSet<DayOfWeek> ignoreDayOfWeeks = null)
        {
            return BuildDateRange(startDate, endDate, ignoreDayOfWeeks).SelectList(x => x.ToDateOnly());
        }

        /// <summary>
        /// Builds a list of weeks and days within each week for the given time range.
        /// </summary>
        /// <returns>A list of lists where each inner list represents the days within a week.</returns>
        /// Example: startDate = new DateTime(2023, 11, 24);  endDate = new DateTime(2023, 12, 6);
        /// Return: 3 weeks like that:
        /// [[24/11/2023, 25/11/2023, 26/11/2023],
        /// [27/11/2023, 28/11/2023, 29/11/2023, 30/11/2023, 01/12/2023, 02/12/2023, 03/12/2023],
        /// [04/12/2023, 05/12/2023, 06/12/2023]]
        public static List<List<DateTime>> BuildWeeksAndDays(DateTime startDate, DateTime endDate, bool isMondayFirstDayOfWeek = true)
        {
            // Determine the nearest first day of the week
            var firstDayOfWeek = isMondayFirstDayOfWeek
                ? startDate.AddDays(DateTimeExtension.MonToSunDayOfWeeks.Monday - startDate.MonToSunDayOfWeek())
                : startDate.AddDays(DayOfWeek.Sunday - startDate.DayOfWeek);

            // List the weeks within the given time range and flatten the list of DateTime
            return Enumerable.Range(0, ((int)(endDate - firstDayOfWeek).TotalDays / 7) + 1)
                .Select(
                    week =>
                        Enumerable.Range(0, 7)
                            .Select(day => firstDayOfWeek.AddDays((week * 7) + day))
                            .Where(date => date >= startDate && date <= endDate)
                            .ToList()
                )
                .ToList();
        }

        public static List<List<DateOnly>> BuildWeeksAndDaysDateOnly(DateTime startDate, DateTime endDate)
        {
            return BuildWeeksAndDays(startDate, endDate)
                .SelectList(week => week.SelectList(date => date.ToDateOnly()).ToList());
        }
    }
}
