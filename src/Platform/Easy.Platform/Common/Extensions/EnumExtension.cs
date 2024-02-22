using System.ComponentModel;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class EnumExtension
{
    /// <summary>
    /// Parses the input Enum to another Enum of type TEnumResult which has the same key.
    /// </summary>
    /// <typeparam name="TEnumResult">The type of the Enum to be returned.</typeparam>
    /// <param name="input">The Enum to be parsed.</param>
    /// <returns>The parsed Enum of type TEnumResult.</returns>
    public static TEnumResult Parse<TEnumResult>(this Enum input)
        where TEnumResult : Enum
    {
        return Util.EnumBuilder.Parse<TEnumResult>(input.ToString("g"));
    }

    /// <summary>
    /// Gets the description of an Enum value.
    /// </summary>
    /// <typeparam name="T">The type of the Enum.</typeparam>
    /// <param name="enumValue">The Enum value.</param>
    /// <returns>
    /// The description of the Enum value if it has a DescriptionAttribute; otherwise, the string representation of the Enum value.
    /// If the type of T is not an Enum, this method returns null.
    /// </returns>
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
