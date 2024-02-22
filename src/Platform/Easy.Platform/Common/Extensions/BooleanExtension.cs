#nullable enable
namespace Easy.Platform.Common.Extensions;

public static class BooleanExtension
{
    /// <summary>
    /// Tries to parse a string to a boolean value. If the parsing fails, it returns false.
    /// </summary>
    /// <param name="boolString">The string to parse.</param>
    /// <returns>True if the string can be parsed to a boolean and the parsed value is true. False otherwise.</returns>
    /// <remarks>
    /// This method uses the bool.TryParse method to attempt to parse the string.
    /// If the parsing is successful and the parsed value is true, it returns true.
    /// If the parsing fails or the parsed value is false, it returns false.
    /// </remarks>
    public static bool TryParseBooleanOrDefault(this string? boolString)
    {
        return bool.TryParse(boolString, out var parsedValue) && parsedValue;
    }
}
