using System.Globalization;
using System.Text.Json;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.JsonSerialization.Converters.Helpers;

/// <summary>
/// Provides helper methods for converting strings to DateTime and DateOnly types.
/// </summary>
/// <remarks>
/// This class is used in various parts of the application where string to DateTime or DateOnly conversion is required.
/// It is utilized in JSON serialization/deserialization and model binding processes.
/// </remarks>
public static class PlatformStringToDateTimeConverterHelper
{
    public static readonly string[] SupportDateOnlyFormats = Util.DateTimeParser.DefaultSupportDateOnlyFormats;

    /// <summary>
    /// Attempts to convert a string to a nullable DateTime.
    /// </summary>
    /// <param name="dateTimeStr">The string representing the DateTime value.</param>
    /// <returns>
    /// A nullable DateTime representing the parsed value if the conversion is successful;
    /// otherwise, returns <c>null</c>.
    /// </returns>
    public static DateTime? TryRead(string dateTimeStr)
    {
        if (dateTimeStr.IsNullOrEmpty()) return null;

        return DeserializeDateTimeValue(dateTimeStr)
            .PipeIf(p => p.Kind == DateTimeKind.Unspecified, p => p.SpecifyKind(DateTimeKind.Utc));
    }

    /// <summary>
    /// Deserializes a DateTime value from the provided string.
    /// </summary>
    /// <param name="dateTimeStr">The string representing the DateTime value.</param>
    /// <returns>The DateTime value parsed from the string.</returns>
    private static DateTime DeserializeDateTimeValue(string dateTimeStr)
    {
        try
        {
            // Try Deserialize like normal standard for normal standard datetime format string
            return dateTimeStr.StartsWith('"') ? JsonSerializer.Deserialize<DateTime>(dateTimeStr) : JsonSerializer.Deserialize<DateTime>($"\"{dateTimeStr}\"");
        }
        catch (Exception)
        {
            try
            {
                return DateTime.ParseExact(dateTimeStr, SupportDateOnlyFormats, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return DateTime.Parse(dateTimeStr!, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None);
            }
        }
    }

    /// <summary>
    /// Attempts to convert a string to a nullable DateOnly.
    /// </summary>
    /// <param name="datetimeOrDateOnlyStr">The string representing the DateOnly value.</param>
    /// <returns>
    /// A nullable DateOnly representing the parsed value if the conversion is successful;
    /// otherwise, returns <c>null</c>.
    /// </returns>
    public static DateOnly? TryReadDateOnly(string datetimeOrDateOnlyStr)
    {
        if (datetimeOrDateOnlyStr.IsNullOrEmpty()) return null;

        try
        {
            try
            {
                return DateTime.ParseExact(datetimeOrDateOnlyStr, SupportDateOnlyFormats, CultureInfo.InvariantCulture).ToDateOnly();
            }
            catch (Exception)
            {
                return DateTime.Parse(datetimeOrDateOnlyStr!, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None).ToDateOnly();
            }
        }
        catch (Exception)
        {
            // Try Deserialize like normal standard for normal standard datetime format string
            return datetimeOrDateOnlyStr.StartsWith('"')
                ? JsonSerializer.Deserialize<DateTime>(datetimeOrDateOnlyStr).ToDateOnly()
                : JsonSerializer.Deserialize<DateTime>($"\"{datetimeOrDateOnlyStr}\"").ToDateOnly();
        }
    }
}
