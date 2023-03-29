using Easy.Platform.Common.Extensions;
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

    public static PlatformValidationResult<T> WithNotFoundException<T>(this PlatformValidationResult<T> val)
    {
        var matchedEnumType = typeof(T).FindMatchedGenericType(typeof(IEnumerable<>));

        var objectType = matchedEnumType != null ? matchedEnumType.GetGenericArguments()[0] : typeof(T);

        return val.WithInvalidException(val => new PlatformNotFoundException(errorMsg: val.ErrorsMsg(), objectType));
    }

    public static async Task<PlatformValidationResult<T>> WithNotFoundExceptionAsync<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.WithNotFoundException();
    }
}
