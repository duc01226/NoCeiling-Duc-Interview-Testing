namespace Easy.Platform.Common.Extensions;

public static class ActionExtension
{
    public static Func<ValueTuple> ToFunc(this Action action)
    {
        return () =>
        {
            action();
            return default;
        };
    }

    public static Func<T, ValueTuple> ToFunc<T>(this Action<T> action)
    {
        return t =>
        {
            action(t);
            return default;
        };
    }

    public static Func<T1, T2, ValueTuple> ToFunc<T1, T2>(this Action<T1, T2> action)
    {
        return (t1, t2) =>
        {
            action(t1, t2);
            return default;
        };
    }

    public static Func<Task<ValueTuple>> ToAsyncFunc(this Func<Task> action)
    {
        return () =>
        {
            return action().Then(() => ValueTuple.Create());
        };
    }

    public static Func<T, Task<ValueTuple>> ToAsyncFunc<T>(this Func<T, Task> action)
    {
        return t =>
        {
            return action(t).Then(() => ValueTuple.Create());
        };
    }

    public static Func<T1, T2, Task<ValueTuple>> ToAsyncFunc<T1, T2>(this Func<T1, T2, Task> action)
    {
        return (t1, t2) =>
        {
            return action(t1, t2).Then(() => ValueTuple.Create());
        };
    }
}
