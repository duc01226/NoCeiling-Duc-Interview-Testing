using Easy.Platform.Common.Validations;

namespace Easy.Platform.Domain.Exceptions.Extensions;

public static class WithDomainExceptionValidationExtension
{
    public static PlatformValidationResult<T> WithDomainException<T>(this PlatformValidationResult<T> val)
    {
        return val.WithInvalidException(val => new PlatformDomainException(val.ErrorsMsg()));
    }

    public static PlatformValidationResult<T> WithDomainValidationException<T>(this PlatformValidationResult<T> val)
    {
        return val.WithInvalidException(val => new PlatformDomainValidationException(val.ErrorsMsg()));
    }

    public static async Task<PlatformValidationResult<T>> WithDomainException<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.WithDomainException();
    }

    public static async Task<PlatformValidationResult<T>> WithDomainValidationException<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.WithDomainValidationException();
    }
}
