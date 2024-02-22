namespace Easy.Platform.Common.Extensions;

public static class TimeOnlyExtension
{
    public static string ToString(this TimeOnly? timeOnly, string format = "HH:mm")
    {
        return timeOnly == null ? string.Empty : timeOnly.Value.ToString(format);
    }
}
