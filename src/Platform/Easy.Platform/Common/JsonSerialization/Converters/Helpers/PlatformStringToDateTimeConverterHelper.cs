using System.Globalization;
using System.Text.Json;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.JsonSerialization.Converters.Helpers;

public static class PlatformStringToDateTimeConverterHelper
{
    public static readonly string[] SupportDateOnlyFormats = Util.DateTimeParser.DefaultSupportDateOnlyFormats;

    public static DateTime? TryRead(string dateTimeStr)
    {
        if (dateTimeStr.IsNullOrEmpty()) return null;

        try
        {
            // Try Deserialize like normal standard for normal standard datetime format string
            return dateTimeStr.StartsWith('"') ? JsonSerializer.Deserialize<DateTime>(dateTimeStr) : JsonSerializer.Deserialize<DateTime>($"\"{dateTimeStr}\"");
        }
        catch (Exception)
        {
            try
            {
                return DateTime.ParseExact(dateTimeStr, SupportDateOnlyFormats, CultureInfo.InvariantCulture)
                    .PipeIf(p => p.Kind == DateTimeKind.Unspecified, p => p.SpecifyKind(DateTimeKind.Utc));
            }
            catch (Exception)
            {
                return DateTime.Parse(dateTimeStr!, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None)
                    .PipeIf(p => p.Kind == DateTimeKind.Unspecified, p => p.SpecifyKind(DateTimeKind.Utc));
            }
        }
    }

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
