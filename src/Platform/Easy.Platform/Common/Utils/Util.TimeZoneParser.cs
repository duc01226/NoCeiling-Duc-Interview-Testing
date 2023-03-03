namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class TimeZoneParser
    {
        public static TimeZoneInfo? TryGetTimeZoneById(string timezoneString)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezoneString);
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
