namespace Easy.Platform.Common.Extensions;

public static class EnsureExtension
{
    public static T EnsureNotNull<T>(this T target, Func<Exception> exception)
    {
        return target.Ensure(target => target != null, exception);
    }

    public static T Ensure<T>(this T target, Func<T, bool> must, Func<Exception> exception)
    {
        return must(target) ? target : throw exception();
    }

    public static T Ensure<T>(this T target, Func<T, bool> must, string errorMsg)
    {
        return must(target) ? target : throw new Exception(errorMsg);
    }
}
