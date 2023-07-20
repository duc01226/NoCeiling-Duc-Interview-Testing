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

        public static DateTimeOffset? ParseDateTimeOffset(string value)
        {
            if (value.IsNullOrEmpty()) return null;

            return DateTimeOffset.TryParse(value, out var dateTimeOffsetValue)
                ? dateTimeOffsetValue
                : null;
        }

        public static DateTime? Parse(string value)
        {
            if (value.IsNullOrEmpty()) return null;

            if (DateTime.TryParse(value, out var tryParsedValue))
                return tryParsedValue.PipeIf(tryParsedValue.Kind == DateTimeKind.Unspecified, _ => _.SpecifyKind(DateTimeKind.Utc));

            if (DateTime.TryParseExact(
                value,
                DefaultSupportDateOnlyFormats,
                null,
                DateTimeStyles.None,
                out var tryParseExactValue))
                return tryParseExactValue.PipeIf(tryParseExactValue.Kind == DateTimeKind.Unspecified, _ => _.SpecifyKind(DateTimeKind.Utc));

            return null;
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
                ? result.PipeIf(result.Kind == DateTimeKind.Unspecified, _ => _.SpecifyKind(DateTimeKind.Utc))
                : null;
        }
    }
}
