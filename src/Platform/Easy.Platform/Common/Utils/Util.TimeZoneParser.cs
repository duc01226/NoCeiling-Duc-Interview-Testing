using TimeZoneConverter;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class TimeZoneParser
    {
        /// Attempts to retrieve a
        /// <see cref="TimeZoneInfo" />
        /// object based on the given time zone string.
        /// It first tries to convert the string as an IANA time zone, and if unsuccessful,
        /// it attempts to retrieve it as a system (Windows) time zone ID.
        /// <param name="timezoneString">
        /// The time zone string, which can be either a Windows time zone ID or an IANA time zone ID.
        /// Example of a Windows time zone ID: "Pacific Standard Time".
        /// Example of an IANA time zone ID: "America/Los_Angeles".
        /// </param>
        /// <returns>
        /// A <see cref="TimeZoneInfo" /> object if the time zone string is valid; otherwise, returns <c>null</c>.
        /// </returns>
        /// <example>
        /// var tzInfoWindows = TryGetTimeZoneById("Pacific Standard Time");
        /// var tzInfoIana = TryGetTimeZoneById("America/Los_Angeles");
        /// </example>
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
