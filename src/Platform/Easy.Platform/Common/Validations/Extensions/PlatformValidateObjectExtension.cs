using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Validations.Extensions;

public static class PlatformValidateObjectExtension
{
    #region Validate

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
        string expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.Validate(
            value,
            () => must(value),
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<TValue, bool> must,
        Func<TValue, string> expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.Validate(
            value,
            () => must(value),
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        Func<bool> must,
        string expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.Validate(
            value,
            must,
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> Validate<TValue>(
        this TValue value,
        bool must,
        string expected,
        string actual)
    {
        return Validate(
            value,
            () => must,
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    #endregion

    #region ValidateNot

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        Func<TValue, bool> mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return PlatformValidationResult<TValue>.ValidateNot(value, () => mustNot(value), errorMsgs);
    }

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        Func<TValue, bool> mustNot,
        Func<TValue, PlatformValidationError> errorMsgs)
    {
        return PlatformValidationResult<TValue>.ValidateNot(value, () => mustNot(value), errorMsgs(value));
    }

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        Func<bool> mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return PlatformValidationResult<TValue>.ValidateNot(value, mustNot, errorMsgs);
    }

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        bool mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return ValidateNot(value, () => mustNot, errorMsgs);
    }


    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        Func<TValue, bool> mustNot,
        string expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.ValidateNot(
            value,
            () => mustNot(value),
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        Func<TValue, bool> mustNot,
        Func<TValue, string> expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.ValidateNot(
            value,
            () => mustNot(value),
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        Func<bool> mustNot,
        string expected,
        string actual)
    {
        return PlatformValidationResult<TValue>.ValidateNot(
            value,
            mustNot,
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    public static PlatformValidationResult<TValue> ValidateNot<TValue>(
        this TValue value,
        bool mustNot,
        string expected,
        string actual)
    {
        return ValidateNot(
            value,
            () => mustNot,
            $"Expected: {expected}".PipeIf(_ => !string.IsNullOrEmpty(actual), _ => _ + $".{Environment.NewLine}Actual: {actual}"));
    }

    #endregion
}
