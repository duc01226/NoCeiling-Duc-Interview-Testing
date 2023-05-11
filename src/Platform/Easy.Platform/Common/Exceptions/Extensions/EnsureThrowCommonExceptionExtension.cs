using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;
using Easy.Platform.Common.Validations.Extensions;

namespace Easy.Platform.Common.Exceptions.Extensions;

public static class EnsureThrowCommonExceptionExtension
{
    public static T EnsurePermissionValid<T>(this PlatformValidationResult<T> val)
    {
        return val.WithPermissionException().EnsureValid();
    }

    public static T EnsurePermissionValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return value.Validate(must, errorMsg).EnsurePermissionValid();
    }

    public static T EnsurePermissionValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return value.Validate(p => must(value).GetResult(), errorMsg).EnsurePermissionValid();
    }

    public static async Task<T> EnsurePermissionValidAsync<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;

        return applicationVal.EnsurePermissionValid();
    }

    public static async Task EnsurePermissionValidAsync(this Task<PlatformValidationResult> valTask)
    {
        var applicationVal = await valTask;
        applicationVal.EnsurePermissionValid();
    }

    public static async Task<T> EnsurePermissionValidAsync<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return value.EnsurePermissionValid(must, errorMsg);
    }

    public static async Task<T> EnsurePermissionValidAsync<T>(this Task<T> valueTask, Func<T, Task<bool>> must, string errorMsg)
    {
        var value = await valueTask;
        return value.EnsurePermissionValid(must, errorMsg);
    }

    [return: NotNull]
    public static T EnsureFound<T>(this T? obj, string errorMsg = null)
    {
        if (obj is Task) throw new Exception($"Target should not be a task. You might want to use {nameof(EnsureFound)} instead.");
        return obj.ValidateFound(errorMsg).WithNotFoundException().EnsureValid();
    }

    public static IEnumerable<T> EnsureFound<T>(this IEnumerable<T> objects, string errorMsg = null)
    {
        return objects.ValidateFound(errorMsg).WithNotFoundException().EnsureValid();
    }

    public static List<T> EnsureFound<T>(this List<T> objects, string errorMsg = null)
    {
        return objects.ValidateFound(errorMsg).WithNotFoundException().EnsureValid();
    }

    public static List<T> EnsureFoundAll<T>(this List<T> objects, List<T> mustFoundAllItems, Func<List<T>, string> notFoundObjectsToErrorMsg)
    {
        return objects.ValidateFoundAll(mustFoundAllItems, notFoundObjectsToErrorMsg).WithNotFoundException().EnsureValid();
    }

    public static List<T> EnsureFoundAllBy<T, TFoundBy>(
        this List<T> objects,
        Func<T, TFoundBy> foundBy,
        List<TFoundBy> toFoundByObjects,
        Func<List<TFoundBy>, string> notFoundByObjectsToErrorMsg)
    {
        return objects.ValidateFoundAllBy(foundBy, toFoundByObjects, notFoundByObjectsToErrorMsg).WithNotFoundException().EnsureValid();
    }

    public static ICollection<T> EnsureFound<T>(this ICollection<T> objects, string errorMsg = null)
    {
        return objects.ValidateFound(errorMsg).WithNotFoundException().EnsureValid();
    }

    [return: NotNull]
    public static T EnsureFound<T>(this T? obj, Func<T, bool> and, string errorMsg = null)
    {
        if (obj is Task) throw new Exception($"Target should not be a task. You might want to use {nameof(EnsureFound)} instead.");
        return obj.ValidateFound(and, errorMsg).WithNotFoundException().EnsureValid();
    }

    public static IQueryable<T> EnsureFoundAny<T>(this IQueryable<T> query, Expression<Func<T, bool>> any, string errorMsg = null)
    {
        return query.ValidateFoundAny(any, errorMsg).WithNotFoundException().EnsureValid();
    }

    [return: NotNull]
    public static async Task<T> EnsureFound<T>(this Task<T?> objectTask, string errorMessage = null)
    {
        var obj = await objectTask;
        return obj.EnsureFound(errorMessage);
    }

    [return: NotNull]
    public static Task<T> EnsureFound<T>(this T? obj, Func<T, Task<bool>> and, string errorMsg = null)
    {
        return obj.ValidateFoundAsync(and, errorMsg).Then(p => p.WithNotFoundException().EnsureValid());
    }

    [return: NotNull]
    public static async Task<T> EnsureFound<T>(this Task<T?> objectTask, Func<T, bool> and, string errorMsg = null)
    {
        var obj = await objectTask;
        return obj.EnsureFound(and, errorMsg);
    }

    [return: NotNull]
    public static async Task<T> EnsureFound<T>(this Task<T?> objectTask, Func<T, Task<bool>> and, string errorMsg = null)
    {
        var obj = await objectTask;
        return await obj.EnsureFound(and, errorMsg);
    }

    public static async Task<IEnumerable<T>> EnsureFound<T>(this Task<IEnumerable<T>> objectsTask, string errorMessage = null)
    {
        var objects = await objectsTask;
        return objects.EnsureFound(errorMessage);
    }

    public static async Task<List<T>> EnsureFound<T>(this Task<List<T>> objectsTask, string errorMessage = null)
    {
        var objects = await objectsTask;
        return objects.EnsureFound(errorMessage);
    }

    public static async Task<List<T>> EnsureFoundAll<T>(this Task<List<T>> objectsTask, List<T> mustFoundAllItems, Func<List<T>, string> errorMsg)
    {
        var objects = await objectsTask;
        return objects.EnsureFoundAll(mustFoundAllItems, errorMsg);
    }

    public static async Task<ICollection<T>> EnsureFound<T>(this Task<ICollection<T>> objectsTask, string errorMessage = null)
    {
        var objects = await objectsTask;
        return objects.EnsureFound(errorMessage);
    }
}
