using System.Globalization;
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
            return DateTime.Parse(dateTimeStr, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None);
        }
        catch (Exception)
        {
            return DateTime.ParseExact(dateTimeStr, SupportDateOnlyFormats, CultureInfo.InvariantCulture);
        }
    }
}
