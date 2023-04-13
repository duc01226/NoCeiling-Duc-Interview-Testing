namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DateRangeBuilder
    {
        public static List<DateTime> BuildDateRange(DateTime startDate, DateTime endDate)
        {
            if (startDate.Date > endDate.Date) return new List<DateTime>();

            return Enumerable.Range(0, 1 + endDate.Subtract(startDate).Days)
                .Select(offset => startDate.AddDays(offset))
                .ToList();
        }
    }
}