namespace WebApi.Common.Extensions;

public static class DateTimeExtension
{
    public static DateTime? TryParseDateTime(this string dateTimeString)
    {
        try
        {
            return DateTime.Parse(dateTimeString);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
