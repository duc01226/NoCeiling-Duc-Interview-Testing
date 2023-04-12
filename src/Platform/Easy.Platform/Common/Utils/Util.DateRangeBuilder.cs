namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DateRangeBuilder
    {
        public static List<DateOnly> BuildDateOnlyRangeFromDateTime(DateTime startDate, DateTime endDate)
        {
            if (startDate.Date > endDate.Date) return new List<DateOnly>();

            return Enumerable.Range(0, 1 + endDate.Subtract(startDate).Days)
                .Select(offset => DateOnly.FromDateTime(startDate.AddDays(offset)))
                .ToList();
        }
    }
}