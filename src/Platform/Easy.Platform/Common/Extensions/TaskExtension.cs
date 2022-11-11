using Easy.Platform.Common.Extensions.WhenCases;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class TaskExtension
{
    /// <summary>
    /// Apply using functional programming to map from Task[A] => (A) => B => Task[B]
    /// </summary>
    public static async Task<TR> Then<T, TR>(
        this Task<T> task,
        Func<T, TR> f)
    {
        return f(await task);
    }

    /// <summary>
    /// Apply using functional programming to map from Task[A] => (A) => B => Task[B]
    /// </summary>
    public static async Task<TR> Then<TR>(
        this Task task,
        Func<TR> f)
    {
        await task;
        return f();
    }

    /// <summary>
    /// Apply using functional programming to map from Task[A] => (A) => Task[B] => Task[B]
    /// </summary>
    public static async Task<TR> Then<T, TR>(
        this Task<T> task,
        Func<T, Task<TR>> nextTask)
    {
        var targetValue = await task;
        return await nextTask(targetValue);
    }

    public static async Task<T> ThenAction<T>(
        this Task<T> task,
        Action<T> action)
    {
        var targetValue = await task;

        action(targetValue);

        return targetValue;
    }

    public static async Task<T> ThenActionAsync<T>(
        this Task<T> task,
        Func<T, Task> nextTask)
    {
        var targetValue = await task;

        await nextTask(targetValue);

        return targetValue;
    }

    public static Task<TR> Then<T, TR>(
        this Task<T> task,
        Func<Exception, TR> faulted,
        Func<T, TR> completed)
    {
        return task.ContinueWith(
            t => t.Status == TaskStatus.Faulted
                ? faulted(t.Exception)
                : completed(t.GetResult()));
    }

    public static async Task<TResult> ThenIf<TTarget, TResult>(
        this Task<TTarget> task,
        Func<TTarget, bool> @if,
        Func<TTarget, Task<TResult>> nextTask) where TTarget : TResult
    {
        var targetValue = await task;
        return @if(targetValue) ? await nextTask(targetValue) : targetValue;
    }

    public static async Task<TResult> ThenIfOrDefault<TTarget, TResult>(
        this Task<TTarget> task,
        Func<TTarget, bool> @if,
        Func<TTarget, Task<TResult>> nextTask,
        TResult defaultValue = default)
    {
        var targetValue = await task;
        return @if(targetValue) ? await nextTask(targetValue) : defaultValue;
    }

    public static Task<ValueTuple<T, T1>> ThenGetWith<T, T1>(this Task<T> task, Func<T, T1> getWith)
    {
        return task.Then(p => (p, getWith(p)));
    }

    public static async Task<ValueTuple<T, T1>> ThenGetWith<T, T1>(this Task<T> task, Func<T, Task<T1>> getWith)
    {
        var value = await task;
        var withValue = await getWith(value);
        return (value, withValue);
    }

    public static async Task<ValueTuple<T, T1, T2>> ThenGetWith<T, T1, T2>(this Task<T> task, Func<T, T1> getWith1, Func<T, T1, T2> getWith2)
    {
        var (value, value1) = await task.ThenGetWith(getWith1);
        return (value, value1, getWith2(value, value1));
    }

    public static async Task<ValueTuple<T, T1, T2>> ThenGetWith<T, T1, T2>(this Task<T> task, Func<T, Task<T1>> getWith1, Func<T, T1, Task<T2>> getWith2)
    {
        var (value, value1) = await task.ThenGetWith(getWith1);
        return (value, value1, await getWith2(value, value1));
    }

    public static Task<List<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks)
    {
        return Task.WhenAll(tasks.ToList()).Then(x => x.ToList());
    }

    public static Task WhenAll(this IEnumerable<Task> tasks)
    {
        var tasksList = tasks.ToList();

        return tasksList.Any() ? Task.WhenAll(tasksList.ToList()) : Task.CompletedTask;
    }

    /// <summary>
    /// Use WaitResult to help if exception to see the stack trace. <br />
    /// Task.Wait() will lead to stack trace lost. <br />
    /// Because the stack trace is technically about where the code is returning to, not where the code came from
    /// </summary>
    public static void WaitResult(this Task task)
    {
        task.GetAwaiter().GetResult();
    }

    public static T GetResult<T>(this Task<T> task)
    {
        return task.GetAwaiter().GetResult();
    }

    public static Task<T> Recover<T>(
        this Task<T> task,
        Func<Exception, T> fallback)
    {
        return task.ContinueWith(
            t => t.Status == TaskStatus.Faulted
                ? fallback(t.Exception)
                : t.GetResult());
    }

    public static Task<T> AsTask<T>(this T t)
    {
        return Task.FromResult(t);
    }

    public static T Wait<T>(this T t, double maxWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(maxWaitSeconds * 1000));

        return t;
    }

    public static T WaitThen<T>(this T t, Action<T> action, double maxWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(maxWaitSeconds * 1000));

        action(t);

        return t;
    }

    public static TResult WaitThen<T, TResult>(
        this T t,
        Func<T, TResult> action,
        double maxWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(maxWaitSeconds * 1000));

        return action(t);
    }

    public static T WaitUntil<T>(
        this T t,
        Func<T, bool> condition,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        Util.TaskRunner.WaitUntil(() => condition(t), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);

        return t;
    }

    public static TResult WaitUntilGetResult<T, TResult>(
        this T t,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetResult(t, getResult, condition, maxWaitSeconds, waitForMsg);
    }

    public static T WaitUntil<T, TStopIfFailResult>(
        this T t,
        Func<T, bool> condition,
        Func<T, TStopIfFailResult> stopIfFail,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntil(t, () => condition(t), stopIfFail, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetResult<T, TResult, TStopIfFailResult>(
        this T t,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Func<T, TStopIfFailResult> stopIfFail,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetResult(t, getResult, condition, stopIfFail, maxWaitSeconds, waitForMsg);
    }

    public static TResult TryWaitUntilGetResult<T, TResult, TStopIfFailResult>(
        this T t,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Func<T, TStopIfFailResult> stopIfFail,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        try
        {
            return Util.TaskRunner.WaitUntilGetResult(t, getResult, condition, stopIfFail, maxWaitSeconds, waitForMsg);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static TResult WaitUntilNotNull<T, TResult>(
        this T t,
        Func<T, TResult> getResult,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return WaitUntilGetResult(t, getResult, _ => _ != null, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilNoException<T, TResult>(
        this T t,
        Func<T, TResult> getResult,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds)
    {
        return Util.TaskRunner.WaitUntilNoException(t, getResult, maxWaitSeconds);
    }

    public static TResult WaitUntilNoException<T, TResult, TStopIfFailResult>(
        this T t,
        Func<T, TResult> getResult,
        Func<T, TStopIfFailResult> stopIfFail,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds)
    {
        return Util.TaskRunner.WaitUntilNoException(t, getResult, stopIfFail, maxWaitSeconds);
    }

    public static T WaitUntilThen<T>(
        this T t,
        Func<T, bool> condition,
        Action<T> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        Util.TaskRunner.WaitUntilThen(() => condition(t), () => action(t), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);

        return t;
    }

    public static TResult WaitUntilThen<T, TResult>(
        this T t,
        Func<T, bool> condition,
        Func<T, TResult> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilThen(() => condition(t), () => action(t), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static async Task<T> WaitUntilThen<T>(
        this T t,
        Func<T, bool> condition,
        Func<Task<T>> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        await Util.TaskRunner.WaitUntilThen(() => condition(t), () => action(), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);

        return t;
    }

    public static async Task<TResult> WaitUntilThen<T, TResult>(
        this T t,
        Func<T, bool> condition,
        Func<T, Task<TResult>> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        return await Util.TaskRunner.WaitUntilThen(() => condition(t), () => action(t), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static TTarget WaitUntilWhenThen<TSource, TTarget>(
        this TSource source,
        Func<TSource, WhenCase<TSource, TTarget>> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilWhenThen(whenDo(source), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static TSource WaitUntilWhenThen<TSource>(
        this TSource source,
        Func<TSource, WhenCase<TSource>> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilWhenThen(whenDo(source), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static TTarget WaitUntilWhenThen<TSource, TTarget>(
        this TSource source,
        WhenCase<TSource, TTarget> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        return source.WaitUntilWhenThen(_ => whenDo, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static TSource WaitUntilWhenThen<TSource>(
        this TSource source,
        WhenCase<TSource> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        return source.WaitUntilWhenThen(_ => whenDo, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static T WaitAndRetryUntil<T>(
        this T t,
        Action<T> action,
        Func<T, bool> until,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds,
        string waitForMsg = null)
    {
        Util.TaskRunner.WaitAndRetryUntil(() => action(t), () => until(t), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);

        return t;
    }
}
