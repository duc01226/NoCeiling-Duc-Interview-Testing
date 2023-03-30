namespace Easy.Platform.Common.Validations.Exceptions.Extensions;

public static class WithExceptionValidationExtension
{
    public static PlatformValidationResult<T> WithValidationException<T>(this PlatformValidationResult<T> val)
    {
        return val.WithInvalidException(val => new PlatformValidationException(val.ErrorsMsg()));
    }

    public static async Task<PlatformValidationResult<T>> WithValidationExceptionAsync<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.WithValidationException();
    }
}
