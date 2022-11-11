namespace Easy.Platform.Common.Extensions;

public static class PipeExtension
{
    public static TResult Pipe<TTarget, TResult>(this TTarget target, Func<TTarget, TResult> fn)
    {
        return fn(target);
    }

    public static TTarget Pipe<TTarget>(this TTarget target, Action<TTarget> fn)
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
