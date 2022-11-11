using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Domain.Exceptions.Extensions;

public static class EnsureThrowDomainExceptionExtension
{
    public static T EnsureDomainLogicValid<T>(this PlatformValidationResult<T> val)
    {
        return val.IsValid ? val.Value : throw new PlatformDomainException(val.ErrorsMsg());
    }

    public static T EnsureDomainValidationValid<T>(this PlatformValidationResult<T> val)
    {
        return val.IsValid ? val.Value : throw new PlatformDomainValidationException(val.ErrorsMsg());
    }

    public static T EnsureDomainValidationValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return must(value) ? value : throw new PlatformDomainValidationException(errorMsg);
    }

    public static T EnsureDomainValidationValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return must(value).GetResult() ? value : throw new PlatformDomainValidationException(errorMsg);
    }

    public static async Task<T> EnsureDomainLogicValid<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsureDomainLogicValid();
    }

    public static async Task<T> EnsureDomainValidationValid<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsureDomainValidationValid();
    }

    public static async Task<T> EnsureDomainValidationValid<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return must(value) ? value : throw new PlatformDomainValidationException(errorMsg);
    }
}
