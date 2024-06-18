#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;
using System.Text.RegularExpressions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Extensions;

public static class StringExtension
{
    /// <summary>
    /// Returns a substring from the start of the string up to the specified maximum length.
    /// </summary>
    /// <param name="strValue">The string to be processed.</param>
    /// <param name="takeMaxLength">The maximum length of the substring to be returned.</param>
    /// <returns>A substring from the start of the string. If the string's length is less than or equal to the specified maximum length, the original string is returned.</returns>
    public static string TakeTop(this string strValue, int takeMaxLength)
    {
        return strValue.Length >= takeMaxLength ? strValue.Substring(0, takeMaxLength) : strValue;
    }

    public static string EnsureNotNullOrWhiteSpace(this string? target, Func<Exception> exception)
    {
        return target.Ensure(must: target => !string.IsNullOrWhiteSpace(target), exception)!;
    }

    public static string EnsureNotNullOrEmpty(this string? target, Func<Exception> exception)
    {
        return target.Ensure(must: target => !string.IsNullOrEmpty(target), exception)!;
    }

    /// <summary>
    /// Deserializes the specified string to the specified type T.
    /// </summary>
    /// <typeparam name="T">The type to which the string will be deserialized.</typeparam>
    /// <param name="strValue">The string to deserialize.</param>
    /// <returns>The deserialized object of type T. If the string is null, the default value of type T is returned.</returns>
    public static T ParseToSerializableType<T>(this string? strValue)
    {
        return strValue != null ? PlatformJsonSerializer.Deserialize<T>(PlatformJsonSerializer.Serialize(strValue)) : default!;
    }

    /// <summary>
    /// Deserializes the specified string to the specified serializable type.
    /// </summary>
    /// <param name="strValue">The string to deserialize.</param>
    /// <param name="serializeType">The type to deserialize the string to.</param>
    /// <returns>The deserialized object of the specified type. If the input string is null, the default value of the specified type is returned.</returns>
    public static object ParseToSerializableType(this string? strValue, Type serializeType)
    {
        return strValue != null ? PlatformJsonSerializer.Deserialize(PlatformJsonSerializer.Serialize(strValue), serializeType) : default!;
    }

    /// <summary>
    /// Returns a substring from the end of the string starting at the specified index.
    /// </summary>
    /// <param name="strValue">The string to be processed.</param>
    /// <param name="fromIndex">The starting index from the end of the string.</param>
    /// <param name="toIndex">The ending index from the start of the string. Default is 0.</param>
    /// <returns>A substring from the end of the string starting at the specified index. If the string's length is less than the specified index, an empty string is returned.</returns>
    public static string SliceFromRight(this string strValue, int fromIndex, int toIndex = 0)
    {
        return strValue.Substring(toIndex, strValue.Length - fromIndex);
    }

    public static bool IsNotNullOrEmpty([NotNullWhen(true)] this string? strValue)
    {
        return !string.IsNullOrEmpty(strValue);
    }

    public static bool IsNotNullOrWhiteSpace([NotNullWhen(true)] this string? strValue)
    {
        return !string.IsNullOrWhiteSpace(strValue);
    }

    public static bool IsNullOrEmpty(this string? strValue)
    {
        return string.IsNullOrEmpty(strValue);
    }

    public static bool IsNullOrWhiteSpace(this string? strValue)
    {
        return string.IsNullOrWhiteSpace(strValue);
    }


    /// <summary>
    /// Removes special characters from the given string, which is intended to be used as a URI.
    /// </summary>
    /// <param name="source">The source string from which special characters should be removed.</param>
    /// <param name="replace">The string that should replace the special characters. Default is an empty string.</param>
    /// <returns>A new string with all special characters replaced by the specified string.</returns>
    [Pure]
    public static string RemoveSpecialCharactersUri(this string source, string replace = "")
    {
        return Regex.Replace(source, @"[^0-9a-zA-Z\._()-\/]+", replace);
    }

    public static T ParseToEnum<T>(this string enumStringValue) where T : Enum
    {
        return (T)Enum.Parse(typeof(T), enumStringValue);
    }

    public static PlatformValidationResult<T> TryParseToEnum<T>(this string enumStringValue) where T : Enum
    {
        try
        {
            return PlatformValidationResult<T>.Valid((T)Enum.Parse(typeof(T), enumStringValue));
        }
        catch
        {
            return PlatformValidationResult<T>.Invalid(default!, $"Can't parse '{enumStringValue}' to enum {nameof(T)}");
        }
    }

    public static string Duplicate(this string duplicateStr, int numberOfDuplicateTimes)
    {
        var strBuilder = new StringBuilder();

        for (var i = 0; i <= numberOfDuplicateTimes; i++) strBuilder.Append(duplicateStr);

        return strBuilder.ToString();
    }

    public static string ConcatString(this IEnumerable<char> chars)
    {
        return string.Concat(chars);
    }

    public static string ConcatString(this string prevStr, ReadOnlySpan<char> chars)
    {
        return string.Concat(prevStr.AsSpan(), chars);
    }

    /// <summary>
    /// Returns a substring from the start of the string up to the next occurrence of the specified character.
    /// </summary>
    /// <param name="str">The string to be processed.</param>
    /// <param name="beforeChar">The character to stop at.</param>
    /// <returns>A substring from the start of the string up to the next occurrence of the specified character. If the character is not found in the string, the original string is returned.</returns>
    public static string TakeUntilNextChar(this string str, char beforeChar)
    {
        return str.Substring(0, str.IndexOf(beforeChar));
    }

    public static string? ToBase64String(this string? str)
    {
        return str != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(str)) : null;
    }

    /// <summary>
    /// Parse value from base64 format to normal utf8 string. <br />
    /// If fail return the original value.
    /// </summary>
    public static string TryFromBase64ToString(this string str)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(str));
        }
        catch (Exception)
        {
            return str;
        }
    }

    public static bool ContainsIgnoreCase(this string? str, string value)
    {
        return str?.Contains(value, StringComparison.InvariantCultureIgnoreCase) == true;
    }

    public static string ToUniqueStr(this string str)
    {
        return str + " " + Ulid.NewUlid();
    }

    public static string ConcatString(this ReadOnlySpan<char> str1, params string[] otherStrings)
    {
        return string.Concat(str1, otherStrings.Aggregate((current, next) => string.Concat(current.AsSpan(), next.AsSpan())).AsSpan());
    }

    /// <summary>
    /// Get Initials of string
    /// Ex: "Nguyen Thanh Phong" => "NTP"
    /// </summary>
    public static string GetAcronym(this string? str, bool upperCase = false)
    {
        if (str.IsNullOrEmpty()) return string.Empty;

        return new string(
            str?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => upperCase ? char.ToUpper(word[0]) : word[0])
                .ToArray());
    }
}
