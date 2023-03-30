namespace Easy.Platform.Common.Validations.Exceptions.Extensions;

public static class EnsureThrowValidationExceptionExtension
{
    public static T EnsureValidationValid<T>(this PlatformValidationResult<T> val)
    {
        return val.IsValid ? val.Value : throw new PlatformValidationException(val.ErrorsMsg());
    }

    public static T EnsureValidationValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return must(value) ? value : throw new PlatformValidationException(errorMsg);
    }

    public static T EnsureValidationValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return must(value).Result ? value : throw new PlatformValidationException(errorMsg);
    }

    public static async Task<T> EnsureValidationValidAsync<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsureValidationValid();
    }

    public static async Task<T> EnsureValidationValidAsync<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return must(value) ? value : throw new PlatformValidationException(errorMsg);
    }
}
