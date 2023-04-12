using System.Globalization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DateTimeParser
    {
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

        public static DateTime? ToPredefinedDateTimeFormat(string dateTime, string[] dateTimeFormats)
        {
            if (dateTime.IsNullOrEmpty()) return null;

            return DateTime.TryParseExact(
                s: dateTime.Trim(),
                dateTimeFormats,
                provider: null,
                style: DateTimeStyles.None,
                out var result)
                ? result
                : null;
        }
    }
}
