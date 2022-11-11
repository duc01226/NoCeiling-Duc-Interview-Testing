using Easy.Platform.Common.Validations;

namespace Easy.Platform.Application.Exceptions.Extensions;

public static class WithInvalidApplicationExceptionValidationExtension
{
    public static PlatformValidationResult<T> WithApplicationException<T>(this PlatformValidationResult<T> val)
    {
        return val.WithInvalidException(val => new PlatformApplicationException(val.ErrorsMsg()));
    }

    public static async Task<PlatformValidationResult<T>> WithApplicationException<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.WithApplicationException();
    }
}
