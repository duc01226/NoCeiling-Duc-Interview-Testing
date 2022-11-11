using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class EnumExtension
{
    /// <summary>
    ///     Parse an Enum to another Enum which has the same key
    /// </summary>
    public static TEnumResult Parse<TEnumResult>(this Enum input)
        where TEnumResult : Enum
    {
        return Util.EnumBuilder.Parse<TEnumResult>(input.ToString("g"));
    }
}
