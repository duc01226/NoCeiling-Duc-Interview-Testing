using TimeZoneConverter;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class TimeZoneParser
    {
        public static TimeZoneInfo TryGetTimeZoneById(string timezoneString)
        {
            try
            {
                if (timezoneString is null) return null;

                var tryAsIanaTimeZoneStr = timezoneString;

                if (TZConvert.TryGetTimeZoneInfo(tryAsIanaTimeZoneStr, out var timeZoneInfo)) return timeZoneInfo;

                return TimeZoneInfo.FindSystemTimeZoneById(timezoneString);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
