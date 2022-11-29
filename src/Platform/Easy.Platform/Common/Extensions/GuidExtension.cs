namespace Easy.Platform.Common.Extensions;

public static class GuidExtension
{
    public static Guid? ToGuid(this string guidStr)
    {
        return Guid.TryParse(guidStr, out var result) ? result : null;
    }
}
