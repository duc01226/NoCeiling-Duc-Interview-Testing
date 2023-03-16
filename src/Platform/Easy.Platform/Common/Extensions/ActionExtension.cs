namespace Easy.Platform.Common.Extensions;

public static class ActionExtension
{
    public static Func<object> ToFunc(this Action action)
    {
        return () =>
        {
            action();
            return default;
        };
    }

    public static Func<T, object> ToFunc<T>(this Action<T> action)
    {
        return t =>
        {
            action(t);
            return default;
        };
    }

    public static Func<T1, T2, object> ToFunc<T1, T2>(this Action<T1, T2> action)
    {
        return (t1, t2) =>
        {
            action(t1, t2);
            return default;
        };
    }

    public static Func<Task<object>> ToAsyncFunc(this Func<Task> action)
    {
        return () =>
        {
            return action().Then(() => (object)ValueTuple.Create());
        };
    }

    public static Func<T, Task<object>> ToAsyncFunc<T>(this Func<T, Task> action)
    {
        return t =>
        {
            return action(t).Then(() => (object)ValueTuple.Create());
        };
    }

    public static Func<T1, T2, Task<object>> ToAsyncFunc<T1, T2>(this Func<T1, T2, Task> action)
    {
        return (t1, t2) =>
        {
            return action(t1, t2).Then(() => (object)ValueTuple.Create());
        };
    }
}
