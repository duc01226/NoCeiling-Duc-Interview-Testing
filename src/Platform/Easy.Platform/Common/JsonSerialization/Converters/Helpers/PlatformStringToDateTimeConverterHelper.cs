using System.Globalization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.JsonSerialization.Converters.Helpers;

public static class PlatformStringToDateTimeConverterHelper
{
    public static readonly string[] SupportDateOnlyFormats =
    {
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "yyyy-MM-dd",
        "dd-MM-yyyy"
    };

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
