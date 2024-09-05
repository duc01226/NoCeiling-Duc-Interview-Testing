using System.Globalization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    /// <summary>
    /// Provides utility methods for parsing and converting date and time values.
    /// </summary>
    public static class DateTimeParser
    {
        /// <summary>
        /// Default supported date-only formats.
        /// </summary>
        public static readonly string[] DefaultSupportDateOnlyFormats =
        [
            "yyyy/MM/dd",
            "dd/MM/yyyy",
            "yyyy-MM-dd",
            "dd-MM-yyyy"
        ];

        /// <summary>
        /// Parses the specified string value into a <see cref="DateTimeOffset" /> instance.
        /// </summary>
        /// <param name="value">The string representation of the date and time.</param>
        /// <returns>
        /// A <see cref="DateTimeOffset" /> instance representing the parsed date and time.
        /// Returns <c>null</c> if the input string is empty or null, or if parsing fails.
        /// </returns>
        public static DateTimeOffset? ParseDateTimeOffset(string value)
        {
            if (value.IsNullOrEmpty()) return null;

            return DateTimeOffset.TryParse(value, out var dateTimeOffsetValue)
                ? dateTimeOffsetValue
                : null;
        }

        /// <summary>
        /// Parses the specified string value into a <see cref="DateTime" /> instance.
        /// </summary>
        /// <param name="value">The string representation of the date and time.</param>
        /// <returns>
        /// A <see cref="DateTime" /> instance representing the parsed date and time.
        /// Returns <c>null</c> if the input string is empty or null, or if parsing fails.
        /// </returns>
        public static DateTime? Parse(string value)
        {
            if (value.IsNullOrEmpty()) return null;

            if (DateTime.TryParse(value, out var tryParsedValue))
                return tryParsedValue.PipeIf(tryParsedValue.Kind == DateTimeKind.Unspecified, t => t.SpecifyKind(DateTimeKind.Utc));

            if (DateTime.TryParseExact(
                value,
                DefaultSupportDateOnlyFormats,
                null,
                DateTimeStyles.None,
                out var tryParseExactValue))
                return tryParseExactValue.PipeIf(tryParseExactValue.Kind == DateTimeKind.Unspecified, t => t.SpecifyKind(DateTimeKind.Utc));

            return null;
        }

        /// <summary>
        /// Converts the specified string representation of a date and time to a <see cref="DateTime" /> instance,
        /// using the specified array of formats and the invariant culture.
        /// </summary>
        /// <param name="dateTime">The string representation of the date and time.</param>
        /// <param name="dateTimeFormats">An optional array of formats that defines the expected formats of the input string.</param>
        /// <returns>
        /// A <see cref="DateTime" /> instance representing the parsed date and time.
        /// Returns <c>null</c> if the input string is empty or null, or if parsing fails.
        /// </returns>
        public static DateTime? ToPredefinedDateTimeFormat(string dateTime, string[] dateTimeFormats = null)
        {
            if (dateTime.IsNullOrEmpty()) return null;

            return DateTime.TryParseExact(
                s: dateTime.Trim(),
                dateTimeFormats ?? DefaultSupportDateOnlyFormats,
                provider: null,
                style: DateTimeStyles.None,
                out var result)
                ? result.PipeIf(result.Kind == DateTimeKind.Unspecified, t => t.SpecifyKind(DateTimeKind.Utc))
                : null;
        }
    }
}
