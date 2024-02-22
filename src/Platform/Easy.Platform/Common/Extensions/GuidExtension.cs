namespace Easy.Platform.Common.Extensions;

public static class GuidExtension
{
    /// <summary>
    /// Converts a string to a Guid? (nullable Guid).
    /// </summary>
    /// <param name="guidStr">The string to convert to a Guid.</param>
    /// <returns>A nullable Guid that represents the converted string. If the string cannot be converted, returns null.</returns>
    public static Guid? ToGuid(this string guidStr)
    {
        return Guid.TryParse(guidStr, out var result) ? result : null;
    }
}
