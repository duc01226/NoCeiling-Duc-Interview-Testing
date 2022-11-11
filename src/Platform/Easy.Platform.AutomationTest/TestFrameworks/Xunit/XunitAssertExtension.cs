using Xunit;

namespace Easy.Platform.AutomationTest.TestFrameworks.Xunit;

public static class XunitAssertExtension
{
    public static T AssertValid<T>(this PlatformValidationResult<T> val)
    {
        if (!val.IsValid)
            Assert.Fail(val.ErrorsMsg());

        return val.Value;
    }

    public static TValue AssertMust<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        PlatformValidationError expected,
        string? actual = null)
    {
        return value.Validate(must, expected, actual).AssertValid();
    }
}
