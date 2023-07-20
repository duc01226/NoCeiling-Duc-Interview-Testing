using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Validations.Exceptions.Extensions;

public static class EnsureThrowValidationExceptionExtension
{
    public static T EnsureValidationValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return must(value) ? value : throw new PlatformValidationException(PlatformValidationResult.Validate(value, () => must(value), errorMsg));
    }

    public static T EnsureValidationValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return must(value).GetResult()
            ? value
            : throw new PlatformValidationException(PlatformValidationResult.Validate(value, () => must(value).GetResult(), errorMsg));
    }

    public static async Task<T> EnsureValidationValidAsync<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsureValid();
    }

    public static async Task EnsureValidationValidAsync(this Task<PlatformValidationResult> valTask)
    {
        var applicationVal = await valTask;
        applicationVal.EnsureValid();
    }

    public static async Task<T> EnsureValidationValidAsync<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return must(value) ? value : throw new PlatformValidationException(errorMsg);
    }
}
