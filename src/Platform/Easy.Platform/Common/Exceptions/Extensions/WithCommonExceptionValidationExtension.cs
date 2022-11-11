using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Exceptions.Extensions;

public static class WithCommonExceptionValidationExtension
{
    public static PlatformValidationResult<T> WithPermissionException<T>(this PlatformValidationResult<T> val)
    {
        return val.WithInvalidException(val => new PlatformPermissionException(val.ErrorsMsg()));
    }

    public static async Task<PlatformValidationResult<T>> WithPermissionExceptionAsync<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.WithPermissionException();
    }
}
