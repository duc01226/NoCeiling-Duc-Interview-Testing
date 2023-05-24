using System.Globalization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DateTimeParser
    {
        public static readonly string[] DefaultSupportDateOnlyFormats =
        {
            "yyyy/MM/dd",
            "dd/MM/yyyy",
            "yyyy-MM-dd",
            "dd-MM-yyyy"
        };

        public static DateTimeOffset? ParseDateTimeOffset(dynamic value)
        {
            return DateTimeOffset.TryParse(value, out DateTimeOffset dateTimeOffsetValue)
                ? dateTimeOffsetValue
                : null;
        }

        public static DateTime? Parse(dynamic value)
        {
            return DateTime.TryParse(value, out DateTime dateTimeOffsetValue)
                ? dateTimeOffsetValue
                : null;
        }

        public static DateTime? ToPredefinedDateTimeFormat(string dateTime, string[] dateTimeFormats = null)
        {
            if (dateTime.IsNullOrEmpty()) return null;

            return DateTime.TryParseExact(
                s: dateTime.Trim(),
                dateTimeFormats ?? DefaultSupportDateOnlyFormats,
                provider: null,
                style: DateTimeStyles.None,
                out var result)
                ? result
                : null;
        }
    }
}
