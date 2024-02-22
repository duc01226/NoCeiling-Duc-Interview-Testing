namespace Easy.Platform.Common.Extensions;

public static class ActionExtension
{
    /// <summary>
    /// Converts an Action delegate to a Func delegate that returns an object.
    /// </summary>
    /// <param name="action">The Action delegate to convert.</param>
    /// <returns>A Func delegate that invokes the Action delegate and returns null.</returns>
    public static Func<object> ToFunc(this Action action)
    {
        return () =>
        {
            action();
            return default;
        };
    }

    /// <summary>
    /// Converts an Action delegate that takes a parameter to a Func delegate that returns an object.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the Action delegate.</typeparam>
    /// <param name="action">The Action delegate to convert.</param>
    /// <returns>A Func delegate that invokes the Action delegate and returns null.</returns>
    public static Func<T, object> ToFunc<T>(this Action<T> action)
    {
        return t =>
        {
            action(t);
            return default;
        };
    }

    /// <summary>
    /// Converts an Action delegate that takes two parameters to a Func delegate that returns an object.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter of the Action delegate.</typeparam>
    /// <typeparam name="T2">The type of the second parameter of the Action delegate.</typeparam>
    /// <param name="action">The Action delegate to convert.</param>
    /// <returns>A Func delegate that invokes the Action delegate and returns null.</returns>
    public static Func<T1, T2, object> ToFunc<T1, T2>(this Action<T1, T2> action)
    {
        return (t1, t2) =>
        {
            action(t1, t2);
            return default;
        };
    }

    /// <summary>
    /// Converts a Func delegate that returns a Task to a Func delegate that returns a Task of object.
    /// </summary>
    /// <param name="action">The Func delegate to convert.</param>
    /// <returns>A Func delegate that invokes the Func delegate and returns a Task of object.</returns>
    public static Func<Task<object>> ToAsyncFunc(this Func<Task> action)
    {
        return () =>
        {
            return action().Then(() => (object)ValueTuple.Create());
        };
    }

    /// <summary>
    /// Converts a Func delegate that takes a parameter and returns a Task to a Func delegate that returns a Task of object.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the Func delegate.</typeparam>
    /// <param name="action">The Func delegate to convert.</param>
    /// <returns>A Func delegate that invokes the Func delegate and returns a Task of object.</returns>
    public static Func<T, Task<object>> ToAsyncFunc<T>(this Func<T, Task> action)
    {
        return t =>
        {
            return action(t).Then(() => (object)ValueTuple.Create());
        };
    }

    /// <summary>
    /// Converts a Func delegate that takes two parameters and returns a Task to a Func delegate that returns a Task of object.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter of the Func delegate.</typeparam>
    /// <typeparam name="T2">The type of the second parameter of the Func delegate.</typeparam>
    /// <param name="action">The Func delegate to convert.</param>
    /// <returns>A Func delegate that invokes the Func delegate and returns a Task of object.</returns>
    public static Func<T1, T2, Task<object>> ToAsyncFunc<T1, T2>(this Func<T1, T2, Task> action)
    {
        return (t1, t2) =>
        {
            return action(t1, t2).Then(() => (object)ValueTuple.Create());
        };
    }
}
