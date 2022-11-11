using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Validations.Extensions;

public static class PlatformValidateObjectExtension
{
    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        params PlatformValidationError[] errorMsgs)
    {
        return PlatformValidationResult<TValue>.Validate(value, () => must(value), errorMsgs);
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        Func<TValue, PlatformValidationError> errorMsgs)
    {
        return PlatformValidationResult<TValue>.Validate(value, () => must(value), errorMsgs(value));
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<bool> must,
        params PlatformValidationError[] errorMsgs)
    {
        return PlatformValidationResult<TValue>.Validate(value, must, errorMsgs);
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        bool must,
        params PlatformValidationError[] errorMsgs)
    {
        return Validate(value, () => must, errorMsgs);
    }


    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        PlatformValidationError expected,
        string actual = null)
    {
        return PlatformValidationResult<TValue>.Validate(
            value,
            () => must(value),
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        Func<TValue, PlatformValidationError> error,
        Func<TValue, string> expected)
    {
        return PlatformValidationResult<TValue>.Validate(value, () => must(value), $"Expected: {error(value)}.{Environment.NewLine}Actual: {expected(value)}");
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<bool> must,
        PlatformValidationError expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.Validate(value, must, $"Expected: {expected}.{Environment.NewLine}Actual: {actual}");
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        bool must,
        PlatformValidationError expected,
        string actual)
    {
        return Validate(value, () => must, $"Expected: {expected}.{Environment.NewLine}Actual: {actual}");
    }
}
