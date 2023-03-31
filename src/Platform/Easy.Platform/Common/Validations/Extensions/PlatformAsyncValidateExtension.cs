using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Validations.Extensions;

public static class PlatformAsyncValidateExtension
{
    public static Task<PlatformValidationResult<TResult>> AndThenValidateAsync<T, TResult>(
        this Task<PlatformValidationResult<T>> sourceValidationTask,
        Func<T, PlatformValidationResult<TResult>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.AndThenValidate(_ => nextValidation(_)));
    }

    public static Task<PlatformValidationResult<TResult>> AndThenValidateAsync<T, TResult>(
        this Task<PlatformValidationResult<T>> sourceValidationTask,
        Func<T, Task<PlatformValidationResult<TResult>>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.AndThenValidateAsync(_ => nextValidation(_)));
    }

    public static Task<PlatformValidationResult> AndAsync(
        this Task<PlatformValidationResult> sourceValidationTask,
        Func<PlatformValidationResult> nextValidation)
    {
        return sourceValidationTask.Then(p => p.And(nextValidation));
    }

    public static Task<PlatformValidationResult> AndAsync(
        this Task<PlatformValidationResult> sourceValidation,
        Func<Task<PlatformValidationResult>> nextValidation)
    {
        return sourceValidation.Then(p => p.AndAsync(nextValidation));
    }

    public static Task<PlatformValidationResult<TValue>> AndAsync<TValue>(
        this Task<PlatformValidationResult<TValue>> sourceValidationTask,
        Func<TValue, PlatformValidationResult<TValue>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.And(nextValidation));
    }

    public static Task<PlatformValidationResult<TValue>> AndAsync<TValue>(
        this Task<PlatformValidationResult<TValue>> sourceValidation,
        Func<TValue, Task<PlatformValidationResult<TValue>>> nextValidation)
    {
        return sourceValidation.Then(p => p.AndAsync(nextValidation));
    }

    public static Task<T> EnsureValidAsync<T>(
        this Task<PlatformValidationResult<T>> sourceValidationTask)
    {
        return sourceValidationTask.Then(p => p.EnsureValid());
    }


    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        bool must,
        params PlatformValidationError[] errors)
    {
        return valueTask.Then(value => value.Validate(must, errors));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        Func<bool> must,
        params PlatformValidationError[] errors)
    {
        return valueTask.Then(value => value.Validate(must, errors));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> must,
        params PlatformValidationError[] errors)
    {
        return valueTask.Then(value => value.Validate(must, errors));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> must,
        Func<TValue, PlatformValidationError> errorMsg)
    {
        return valueTask.Then(value => value.Validate(must, errorMsg));
    }

    public static Task<PlatformValidationResult<List<T>>> ThenValidateFoundAllAsync<T>(
        this Task<List<T>> objectsTask,
        List<T> mustFoundAllItems,
        Func<List<T>, string> notFoundObjectsToErrorMsg)
    {
        return objectsTask.Then(p => p.ValidateFoundAll(mustFoundAllItems, notFoundObjectsToErrorMsg));
    }

    public static Task<PlatformValidationResult<List<T>>> ThenValidateFoundAllByAsync<T, TFoundBy>(
        this Task<List<T>> objectsTask,
        Func<T, TFoundBy> foundBy,
        List<TFoundBy> toFoundByObjects,
        Func<List<TFoundBy>, string> notFoundByObjectsToErrorMsg)
    {
        return objectsTask.Then(p => p.ValidateFoundAllBy(foundBy, toFoundByObjects, notFoundByObjectsToErrorMsg));
    }

    public static Task<PlatformValidationResult<T>> ThenValidateFoundAsync<T>(this Task<T?> objTask, string errorMsg = null)
    {
        return objTask.Then(p => p.ValidateFound(errorMsg));
    }

    public static Task<PlatformValidationResult<IEnumerable<T>>> ThenValidateFoundAsync<T>(this Task<IEnumerable<T>> objectsTask, string errorMsg = null)
    {
        return objectsTask.Then(p => p.ValidateFound(errorMsg));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, () => mustNot(value), errorMsgs));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> mustNot,
        Func<TValue, PlatformValidationError> errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, () => mustNot(value), errorMsgs(value)));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        Func<bool> mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, mustNot, errorMsgs));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        bool mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, mustNot, errorMsgs));
    }
}
