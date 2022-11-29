using System.ComponentModel;
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

    public static string GetDescription<T>(this T enumValue)
        where T : struct, IConvertible
    {
        if (!typeof(T).IsEnum) return null;

        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString()!);

        var descAttrs = fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true);

        if (descAttrs?.Length > 0) return ((DescriptionAttribute)descAttrs[0]).Description;

        return enumValue.ToString();
    }
}
