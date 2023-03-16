using Xunit;

namespace Easy.Platform.AutomationTest.Helpers;

public static class AssertHelper
{
    public static string Failed(string generalMsg, string expected, string actual)
    {
        return $"{generalMsg}.{Environment.NewLine}Expected: {expected}.{Environment.NewLine}Actual: {actual}";
    }

    public static string Failed(string expected, string? actual = null)
    {
        return $"Expected: {expected}" + (actual != null ? $".{Environment.NewLine}Actual: {actual}" : "");
    }

    public static T AssertValid<T>(this PlatformValidationResult<T> val)
    {
        if (!val.IsValid)
            Assert.Fail(message: val.ErrorsMsg());

        return val.Value;
    }

    public static TValue AssertMust<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        PlatformValidationError expected,
        string? actual = null)
    {
        return value.Validate(must, expected, actual: actual).AssertValid();
    }

    public static TValue AssertMustNot<TValue>(
        this TValue value,
        Func<TValue, bool> mustNot,
        PlatformValidationError expected,
        string? actual = null)
    {
        return value.ValidateNot(mustNot, expected, actual: actual).AssertValid();
    }

    public static TValue AssertFail<TValue>(
        this TValue value,
        PlatformValidationError expected,
        string? actual = null)
    {
        Assert.Fail(message: Failed(expected, actual));

        return value;
    }

    public static TValue AssertSuccess<TValue>(
        this TValue value)
    {
        return value;
    }
}
