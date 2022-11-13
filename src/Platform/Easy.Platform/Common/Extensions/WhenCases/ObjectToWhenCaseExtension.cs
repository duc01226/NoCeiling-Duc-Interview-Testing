namespace Easy.Platform.Common.Extensions.WhenCases;

public static class ObjectToWhenCaseExtension
{
    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        params WhenCase<TSource, TTarget>.CaseItem[] cases)
    {
        return new WhenCase<TSource, TTarget>(source, cases);
    }

    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        Func<TSource, bool> @case,
        Func<TSource, TTarget> then)
    {
        return new WhenCase<TSource, TTarget>(source, @case, then);
    }

    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        Func<TSource, bool> @case,
        Func<TSource, Task<TTarget>> then)
    {
        return new WhenCase<TSource, TTarget>(source, @case, then);
    }

    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        Func<bool> @case,
        Func<TSource, TTarget> then)
    {
        return new WhenCase<TSource, TTarget>(source, _ => @case(), then);
    }

    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        Func<bool> @case,
        Func<Task<TTarget>> then)
    {
        return new WhenCase<TSource, TTarget>(source, _ => @case(), _ => then());
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        params WhenCase<TSource, ValueTuple>.CaseItem[] cases)
    {
        return new WhenCase<TSource>(source, cases);
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<TSource, bool> @case,
        Action<TSource> then)
    {
        return new WhenCase<TSource>(source, @case, then.ToFunc());
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<TSource, bool> @case,
        Action then)
    {
        return new WhenCase<TSource>(source, @case, _ => then.ToFunc()());
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<TSource, bool> @case,
        Func<TSource, Task> then)
    {
        return new WhenCase<TSource>(source, @case, _ => then.ToAsyncFunc()(_));
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<TSource, bool> @case,
        Func<Task> then)
    {
        return new WhenCase<TSource>(source, @case, _ => then.ToAsyncFunc()());
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<bool> @case,
        Action<TSource> then)
    {
        return new WhenCase<TSource>(source, _ => @case(), then.ToFunc());
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<bool> @case,
        Func<Task> then)
    {
        return new WhenCase<TSource>(source, _ => @case(), _ => then.ToAsyncFunc()());
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public static WhenCase<TSource, TTarget> WhenIs<TSource, TSourceIs, TTarget>(
        this TSource source,
        Func<TSourceIs, TTarget> then) where TSourceIs : class
    {
        return WhenCase<TSource, TTarget>.WhenIs(source, then);
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public static WhenCase<TSource, TTarget> WhenIs<TSource, TSourceIs, TTarget>(
        this TSource source,
        Func<TSourceIs, Task<TTarget>> then) where TSourceIs : class
    {
        return WhenCase<TSource, TTarget>.WhenIs(source, then);
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public static WhenCase<TSource> WhenIs<TSource, TSourceIs>(
        this TSource source,
        Action<TSourceIs> then) where TSourceIs : class
    {
        return WhenCase<TSource>.WhenIs(source, then);
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public static WhenCase<TSource> WhenIs<TSource, TSourceIs>(
        this TSource source,
        Func<TSourceIs, Task> then) where TSourceIs : class
    {
        return WhenCase<TSource>.WhenIs(source, then);
    }

    public static WhenCase<TSource, TTarget> WhenValue<TSource, TTarget>(
        this TSource source,
        TSource @case,
        Func<TSource, TTarget> then)
    {
        return WhenCase<TSource, TTarget>.WhenValue(source, @case, then);
    }

    public static WhenCase<TSource, TTarget> WhenValue<TSource, TTarget>(
        this TSource source,
        TSource @case,
        Func<TSource, Task<TTarget>> then)
    {
        return WhenCase<TSource, TTarget>.WhenValue(source, @case, then);
    }

    public static WhenCase<TSource> WhenValue<TSource>(
        this TSource source,
        TSource @case,
        Action<TSource> then)
    {
        return WhenCase<TSource>.WhenValue(source, @case, then);
    }

    public static WhenCase<TSource> WhenValue<TSource>(
        this TSource source,
        TSource @case,
        Func<TSource, Task> then)
    {
        return WhenCase<TSource>.WhenValue(source, @case, then);
    }

    public static WhenCase<TSource> WhenValue<TSource>(
        this TSource source,
        TSource @case,
        Action then)
    {
        return WhenCase<TSource>.WhenValue(source, @case, then);
    }

    public static WhenCase<TSource> WhenValue<TSource>(
        this TSource source,
        TSource @case,
        Func<Task> then)
    {
        return WhenCase<TSource>.WhenValue(source, @case, then);
    }

    #region Async @case

    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        Func<TSource, Task<bool>> @case,
        Func<TSource, TTarget> then)
    {
        return new WhenCase<TSource, TTarget>(source, @case, then);
    }


    public static WhenCase<TSource, TTarget> When<TSource, TTarget>(
        this TSource source,
        Func<Task<bool>> @case,
        Func<TSource, TTarget> then)
    {
        return new WhenCase<TSource, TTarget>(source, @case, then);
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<TSource, Task<bool>> @case,
        Action<TSource> then)
    {
        return new WhenCase<TSource>(source, @case, then);
    }

    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<TSource, Task<bool>> @case,
        Action then)
    {
        return new WhenCase<TSource>(source, @case, then);
    }


    public static WhenCase<TSource> When<TSource>(
        this TSource source,
        Func<Task<bool>> @case,
        Action<TSource> then)
    {
        return new WhenCase<TSource>(source, @case, then);
    }

    #endregion
}
