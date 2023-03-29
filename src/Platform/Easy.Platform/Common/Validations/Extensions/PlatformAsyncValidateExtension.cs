using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Validations.Extensions;

public static class PlatformAsyncValidateExtension
{
    public static Task<PlatformValidationResult<TResult>> ThenValidate<T, TResult>(
        this Task<PlatformValidationResult<T>> sourceValidationTask,
        Func<T, PlatformValidationResult<TResult>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.AndThen(_ => nextValidation(_)));
    }

    public static Task<PlatformValidationResult> ThenValidate(
        this Task<PlatformValidationResult> sourceValidationTask,
        Func<PlatformValidationResult> nextValidation)
    {
        return sourceValidationTask.Then(p => p.And(nextValidation));
    }

    public static Task<PlatformValidationResult<TResult>> ThenValidateAsync<T, TResult>(
        this Task<PlatformValidationResult<T>> sourceValidationTask,
        Func<T, Task<PlatformValidationResult<TResult>>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.AndThenAsync(_ => nextValidation(_)));
    }

    public static Task<PlatformValidationResult> ThenValidateAsync(
        this Task<PlatformValidationResult> sourceValidation,
        Func<Task<PlatformValidationResult>> nextValidation)
    {
        return sourceValidation.Then(p => p.AndAsync(nextValidation));
    }
}
