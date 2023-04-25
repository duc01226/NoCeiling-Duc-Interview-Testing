using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DateRangeBuilder
    {
        public static List<DateTime> BuildDateRange(DateTime startDate, DateTime endDate, HashSet<DayOfWeek> ignoreDayOfWeeks = null)
        {
            if (startDate.Date > endDate.Date) return new List<DateTime>();

            return Enumerable.Range(0, 1 + endDate.Subtract(startDate).Days)
                .Select(offset => startDate.AddDays(offset))
                .WhereIf(ignoreDayOfWeeks != null, date => !ignoreDayOfWeeks.Contains(date.DayOfWeek))
                .ToList();
        }
    }
}
