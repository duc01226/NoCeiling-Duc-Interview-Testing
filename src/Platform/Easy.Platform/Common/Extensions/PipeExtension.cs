namespace Easy.Platform.Common.Extensions;

public static class PipeExtension
{
    /// <summary>
    /// Transforms the input target by applying the specified function.
    /// </summary>
    /// <typeparam name="TTarget">The type of the input target.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="target">The input target to be transformed.</param>
    /// <param name="fn">The function to apply to the input target.</param>
    /// <returns>The result of applying the function to the input target.</returns>
    public static TResult Pipe<TTarget, TResult>(this TTarget target, Func<TTarget, TResult> fn)
    {
        return fn(target);
    }

    /// <summary>
    /// Executes a specified function on the target object and returns the target object.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the function.</typeparam>
    /// <param name="target">The target object on which the function is executed.</param>
    /// <param name="fn">The function to be executed on the target object.</param>
    /// <returns>The target object after the function has been executed.</returns>
    public static TTarget PipeAction<TTarget, TResult>(this TTarget target, Func<TTarget, TResult> fn)
    {
        fn(target);

        return target;
    }

    /// <summary>
    /// Executes a specified action on the target object and returns the same object.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    /// <param name="target">The target object on which the action is performed.</param>
    /// <param name="fn">The action to be performed on the target object.</param>
    /// <returns>The target object after the action has been performed on it.</returns>
    public static TTarget PipeAction<TTarget>(this TTarget target, Action<TTarget> fn)
    {
        fn(target);

        return target;
    }

    /// <summary>
    /// Pipe when condition is matched, or else return itself
    /// </summary>
    public static TResult PipeIf<TTarget, TResult>(
        this TTarget target,
        bool when,
        Func<TTarget, TResult> thenPipe) where TTarget : TResult
    {
        return when ? thenPipe(target) : target;
    }

    /// <summary>
    /// Pipe when condition is matched, or else return itself
    /// </summary>
    public static TResult PipeIf<TTarget, TResult>(
        this TTarget target,
        Func<TTarget, bool> when,
        Func<TTarget, TResult> thenPipe) where TTarget : TResult
    {
        return when(target) ? thenPipe(target) : target;
    }

    /// <summary>
    /// Pipe if the value is not null,  or else return default value of return pipe type
    /// </summary>
    public static TResult PipeIfNotNull<TTarget, TResult>(
        this TTarget target,
        Func<TTarget, TResult> thenPipe,
        TResult defaultValue = default)
    {
        return target != null ? thenPipe(target) : defaultValue;
    }

    /// <summary>
    /// Pipe if the value is not null,  or else return default value of return pipe type
    /// </summary>
    public static TResult PipeIfNotNull<TTarget, TResult>(
        this TTarget? target,
        Func<TTarget, TResult> thenPipe,
        TResult defaultValue = default) where TTarget : struct
    {
        return target != null ? thenPipe(target.Value) : defaultValue;
    }

    /// <summary>
    /// Pipe if the value is not null,  or else return default value of return pipe type
    /// </summary>
    public static async Task<TResult> PipeIfNotNull<TTarget, TResult>(
        this TTarget target,
        Func<TTarget, Task<TResult>> thenPipe,
        TResult defaultValue = default)
    {
        return target != null ? await thenPipe(target) : defaultValue;
    }

    /// <summary>
    /// Pipe if condition is matched, or else return default value of return pipe type
    /// </summary>
    public static TResult PipeIfOrDefault<TTarget, TResult>(
        this TTarget target,
        bool when,
        Func<TTarget, TResult> thenPipe,
        TResult defaultValue = default)
    {
        return when ? thenPipe(target) : defaultValue;
    }

    /// <summary>
    /// Pipe if condition is matched, or else return default value of return pipe type
    /// </summary>
    public static TResult PipeIfOrDefault<TTarget, TResult>(
        this TTarget target,
        Func<TTarget, bool> when,
        Func<TTarget, TResult> thenPipe,
        TResult defaultValue = default)
    {
        return when(target) ? thenPipe(target) : defaultValue;
    }

    /// <summary>
    /// Pipe if target value is not null, or else return default value of return pipe type
    /// </summary>
    public static TResult PipeIfNotNullOrDefault<TTarget, TResult>(
        this TTarget target,
        Func<TTarget, TResult> thenPipe,
        TResult defaultValue = default)
    {
        return target != null ? thenPipe(target) : defaultValue;
    }

    /// <summary>
    /// Pipe if target value is not null, or else return default value of return pipe type
    /// </summary>
    public static async Task<TResult> PipeIfNotNullOrDefault<TTarget, TResult>(
        this TTarget target,
        Func<TTarget, Task<TResult>> thenPipe,
        TResult defaultValue = default)
    {
        return target != null ? await thenPipe(target) : defaultValue;
    }
}
