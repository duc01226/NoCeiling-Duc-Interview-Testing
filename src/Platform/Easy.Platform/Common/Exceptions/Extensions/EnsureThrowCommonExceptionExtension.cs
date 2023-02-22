using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Exceptions.Extensions;

public static class EnsureThrowCommonExceptionExtension
{
    public static T EnsurePermissionValid<T>(this PlatformValidationResult<T> val)
    {
        return val.IsValid ? val.Value : throw new PlatformPermissionException(val.ErrorsMsg());
    }

    public static T EnsurePermissionValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return must(value) ? value : throw new PlatformPermissionException(errorMsg);
    }

    public static T EnsurePermissionValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return must(value).GetResult() ? value : throw new PlatformPermissionException(errorMsg);
    }

    public static async Task<T> EnsurePermissionValid<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsurePermissionValid();
    }

    public static async Task EnsurePermissionValid(this Task<PlatformValidationResult> valTask)
    {
        var applicationVal = await valTask;
        applicationVal.EnsurePermissionValid();
    }

    public static async Task<T> EnsurePermissionValid<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return value.EnsurePermissionValid(must, errorMsg);
    }

    public static async Task<T> EnsurePermissionValid<T>(this Task<T> valueTask, Func<T, Task<bool>> must, string errorMsg)
    {
        var value = await valueTask;
        return value.EnsurePermissionValid(must, errorMsg);
    }

    public static T EnsureFound<T>(this T obj, string errorMsg = null)
    {
        return obj != null ? obj : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static IEnumerable<T> EnsureFound<T>(this IEnumerable<T> objects, string errorMsg = null)
    {
        var objectsList = objects?.ToList();
        return objectsList?.Any() == true ? objectsList : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static List<T> EnsureFound<T>(this List<T> objects, string errorMsg = null)
    {
        return objects?.Any() == true ? objects : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static List<T> EnsureFoundAll<T>(this List<T> objects, List<T> toFoundObjects, Func<List<T>, string> errorMsg)
    {
        var notFoundObjects = toFoundObjects.Except(objects ?? new List<T>()).ToList();

        return notFoundObjects.Any() ? throw new PlatformNotFoundException(errorMsg(notFoundObjects), typeof(T)) : objects;
    }

    public static List<T> EnsureFoundAllBy<T, TFoundBy>(
        this List<T> objects,
        Func<T, TFoundBy> foundBy,
        List<TFoundBy> toFoundByObjects,
        Func<List<TFoundBy>, string> errorMsg)
    {
        var notFoundByObjects = toFoundByObjects.Except(objects.Select(foundBy) ?? new List<TFoundBy>()).ToList();

        return notFoundByObjects.Any() ? throw new PlatformNotFoundException(errorMsg(notFoundByObjects), typeof(T)) : objects;
    }

    public static ICollection<T> EnsureFound<T>(this ICollection<T> objects, string errorMsg = null)
    {
        var objectsList = objects?.ToList();
        return objectsList?.Any() == true ? objectsList : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static T EnsureFound<T>(this T obj, Func<T, bool> and, string errorMsg = null)
    {
        return obj != null && and(obj)
            ? obj
            : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static async Task<T> EnsureFound<T>(this T obj, Func<T, Task<bool>> and, string errorMsg = null)
    {
        return obj != null && await and(obj)
            ? obj
            : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static IQueryable<T> EnsureFound<T>(this IQueryable<T> query, Expression<Func<T, bool>> any, string errorMsg = null)
    {
        return query.Any(any) ? query : throw new PlatformNotFoundException(errorMsg, typeof(T));
    }

    public static async Task<T> EnsureFound<T>(this Task<T> objectTask, string errorMessage = null)
    {
        var obj = await objectTask;
        return obj.EnsureFound(errorMessage);
    }

    public static async Task<T> EnsureFound<T>(this Task<T> objectTask, Func<T, bool> and, string errorMsg = null)
    {
        var obj = await objectTask;
        return obj.EnsureFound(and, errorMsg);
    }

    public static async Task<T> EnsureFound<T>(this Task<T> objectTask, Func<T, Task<bool>> and, string errorMsg = null)
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

    public static async Task<List<T>> EnsureFoundAll<T>(this Task<List<T>> objectsTask, List<T> toFoundObjects, Func<List<T>, string> errorMsg)
    {
        var objects = await objectsTask;
        return objects.EnsureFoundAll(toFoundObjects, errorMsg);
    }

    public static async Task<ICollection<T>> EnsureFound<T>(this Task<ICollection<T>> objectsTask, string errorMessage = null)
    {
        var objects = await objectsTask;
        return objects.EnsureFound(errorMessage);
    }
}
