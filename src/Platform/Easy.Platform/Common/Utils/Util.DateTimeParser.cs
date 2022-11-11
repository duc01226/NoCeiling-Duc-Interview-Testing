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
    }
}
