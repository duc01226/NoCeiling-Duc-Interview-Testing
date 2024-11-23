namespace Easy.Platform.Common.Extensions;

public static class EnsureExtension
{
    /// <summary>
    /// Ensures that the target object is not null.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object.</param>
    /// <param name="exception">A function that produces the exception to throw if the target object is null.</param>
    /// <returns>The target object if it is not null.</returns>
    /// <exception cref="Exception">The exception produced by the exception function if the target object is null.</exception>
    public static T EnsureNotNull<T>(this T target, Func<Exception> exception)
    {
        return target.Ensure(target => target is not null, exception);
    }

    /// <summary>
    /// Ensures that the target object meets a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object.</param>
    /// <param name="must">A function that defines the condition the target object must meet.</param>
    /// <param name="exception">A function that produces the exception to throw if the target object does not meet the condition.</param>
    /// <returns>The target object if it meets the condition.</returns>
    /// <exception cref="Exception">The exception produced by the exception function if the target object does not meet the condition.</exception>
    public static T Ensure<T>(this T target, Func<T, bool> must, Func<Exception> exception)
    {
        return must(target) ? target : throw exception();
    }

    /// <summary>
    /// Ensures that the target object meets a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object.</param>
    /// <param name="must">A function that defines the condition the target object must meet.</param>
    /// <param name="errorMsg">The error message to include in the exception if the target object does not meet the condition.</param>
    /// <returns>The target object if it meets the condition.</returns>
    /// <exception cref="Exception">An exception with the specified error message if the target object does not meet the condition.</exception>
    public static T Ensure<T>(this T target, Func<T, bool> must, string errorMsg)
    {
        return must(target) ? target : throw new Exception(errorMsg);
    }
}
