using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Application.Exceptions.Extensions;

public static class EnsureThrowApplicationExceptionExtension
{
    public static T EnsureApplicationLogicValid<T>(this PlatformValidationResult<T> applicationVal)
    {
        return applicationVal.IsValid ? applicationVal.Value : throw new PlatformApplicationException(applicationVal.ErrorsMsg());
    }

    public static T EnsureApplicationLogicValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return must(value) ? value : throw new PlatformApplicationException(errorMsg);
    }

    public static T EnsureApplicationLogicValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return must(value).GetResult() ? value : throw new PlatformApplicationException(errorMsg);
    }

    public static async Task<T> EnsureApplicationLogicValidAsync<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return value.EnsureApplicationLogicValid(must, errorMsg);
    }

    public static async Task<T> EnsureApplicationLogicValidAsync<T>(this Task<PlatformValidationResult<T>> applicationValTask)
    {
        var applicationVal = await applicationValTask;
        return applicationVal.EnsureApplicationLogicValid();
    }

    public static async Task<object> EnsureApplicationLogicValidAsync(this Task<PlatformValidationResult> applicationValTask)
    {
        var applicationVal = await applicationValTask;
        return applicationVal.EnsureApplicationLogicValid();
    }
}
