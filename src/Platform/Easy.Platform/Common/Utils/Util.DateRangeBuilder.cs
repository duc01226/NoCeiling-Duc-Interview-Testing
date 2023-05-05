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
            if (startDate.Date > endDate.Date) return new List<DateTime>();

            return Enumerable.Range(0, endDate.Date.Subtract(startDate.Date).Days + 1)
                .Select(offset => startDate.AddDays(offset))
                .WhereIf(ignoreDayOfWeeks != null, date => !ignoreDayOfWeeks.Contains(date.DayOfWeek))
                .ToList();
        }
    }
}
