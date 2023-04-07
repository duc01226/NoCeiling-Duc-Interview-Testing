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
        var taskResult = await task;
        return await nextTask(taskResult);
    }

    public static async Task<TR> Then<T1, T2, TR>(
        this Task<ValueTuple<T1, T2>> task,
        Func<T1, T2, Task<TR>> nextTask)
    {
        var taskResult = await task;
        return await nextTask(taskResult.Item1, taskResult.Item2);
    }

    public static async Task<TR> Then<T1, T2, T3, TR>(
        this Task<ValueTuple<T1, T2, T3>> task,
        Func<T1, T2, T3, Task<TR>> nextTask)
    {
        var taskResult = await task;
        return await nextTask(taskResult.Item1, taskResult.Item2, taskResult.Item3);
    }

    public static async Task<TR> Then<T1, T2, T3, T4, TR>(
        this Task<ValueTuple<T1, T2, T3, T4>> task,
        Func<T1, T2, T3, T4, Task<TR>> nextTask)
    {
        var taskResult = await task;
        return await nextTask(taskResult.Item1, taskResult.Item2, taskResult.Item3, taskResult.Item4);
    }

    public static async Task<TR> Then<T1, T2, T3, T4, T5, TR>(
        this Task<ValueTuple<T1, T2, T3, T4, T5>> task,
        Func<T1, T2, T3, T4, T5, Task<TR>> nextTask)
    {
        var taskResult = await task;
        return await nextTask(taskResult.Item1, taskResult.Item2, taskResult.Item3, taskResult.Item4, taskResult.Item5);
    }

    public static async Task<T> ThenSideEffectAction<T>(
        this Task<T> task,
        Action<T> action)
    {
        var targetValue = await task;

        action(targetValue);

        return targetValue;
    }

    public static async Task<T> ThenSideEffectActionAsync<T>(
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

    public static Task<T> ToTask<T>(this T t)
    {
        return Task.FromResult(t);
    }

    public static T Wait<T>(this T target, double maxWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(maxWaitSeconds * 1000));

        return target;
    }


    /// <summary>
    /// Wait a period of time then do a given action
    /// </summary>
    public static T WaitThen<T>(this T target, Action<T> action, double maxWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(maxWaitSeconds * 1000));

        action(target);

        return target;
    }

    /// <summary>
    /// Wait a period of time then do a given action
    /// </summary>
    public static TResult WaitThen<T, TResult>(
        this T target,
        Func<T, TResult> action,
        double maxWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(maxWaitSeconds * 1000));

        return action(target);
    }

    /// <inheritdoc cref="Util.TaskRunner.WaitUntil{T}" />
    public static T WaitUntil<T>(
        this T target,
        Func<T, bool> condition,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        Util.TaskRunner.WaitUntil(() => condition(target), maxWaitSeconds, waitForMsg: waitForMsg);

        return target;
    }

    /// <inheritdoc cref="Util.TaskRunner.WaitUntil{T}" />
    public static T WaitUntil<T>(
        this T target,
        Func<bool> condition,
        Action<T> continueWaitOnlyWhen,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntil(target, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetValidResult<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetValidResult(target, getResult, condition, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetValidResult<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Action<T> continueWaitOnlyWhen,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetValidResult(target, getResult, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    /// <inheritdoc cref="Util.TaskRunner.WaitUntil{T}" />
    public static T WaitUntil<T, TAny>(
        this T target,
        Func<T, bool> condition,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntil(target, () => condition(target), continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    /// <inheritdoc cref="Util.TaskRunner.WaitUntil{T}" />
    public static T WaitUntil<T>(
        this T target,
        Func<T, bool> condition,
        Action<T> continueWaitOnlyWhen,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntil(target, () => condition(target), continueWaitOnlyWhen.ToFunc(), maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetValidResult<T, TResult, TAny>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetValidResult(target, getResult, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    /// <summary>
    /// WaitUntilGetValidResult. If failed return default value.
    /// </summary>
    public static TResult TryWaitUntilGetValidResult<T, TResult, TAny>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        try
        {
            return Util.TaskRunner.WaitUntilGetValidResult(target, getResult, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static TResult WaitUntilNotNull<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return WaitUntilGetValidResult(target, getResult, _ => _ != null, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetSuccess<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccess(target, getResult, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetSuccess<T, TResult, TAny>(
        this T target,
        Func<T, TResult> getResult,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccess(target, getResult, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    public static TResult WaitUntilGetSuccess<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        Action<T> continueWaitOnlyWhen,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccess(target, getResult, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    public static T WaitUntilToDo<T>(
        this T target,
        Func<T, bool> condition,
        Action<T> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        Util.TaskRunner.WaitUntilToDo(() => condition(target), () => action(target), maxWaitSeconds, waitForMsg: waitForMsg);

        return target;
    }

    public static TResult WaitUntilToDo<T, TResult>(
        this T target,
        Func<T, bool> condition,
        Func<T, TResult> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilToDo(() => condition(target), () => action(target), maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public static async Task<T> WaitUntilToDo<T>(
        this T target,
        Func<T, bool> condition,
        Func<Task<T>> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        await Util.TaskRunner.WaitUntilToDo(() => condition(target), () => action(), maxWaitSeconds, waitForMsg: waitForMsg);

        return target;
    }

    public static async Task<TResult> WaitUntilToDo<T, TResult>(
        this T target,
        Func<T, bool> condition,
        Func<T, Task<TResult>> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return await Util.TaskRunner.WaitUntilToDo(() => condition(target), () => action(target), maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public static TTarget WaitUntilHasMatchedCase<TSource, TTarget>(
        this TSource source,
        Func<TSource, WhenCase<TSource, TTarget>> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilHasMatchedCase(whenDo(source), maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public static TSource WaitUntilHasMatchedCase<TSource>(
        this TSource source,
        Func<TSource, WhenCase<TSource>> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilHasMatchedCase(whenDo(source), maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public static TTarget WaitUntilHasMatchedCase<TSource, TTarget>(
        this TSource source,
        WhenCase<TSource, TTarget> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return source.WaitUntilHasMatchedCase(_ => whenDo, maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public static TSource WaitUntilHasMatchedCase<TSource>(
        this TSource source,
        WhenCase<TSource> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return source.WaitUntilHasMatchedCase(_ => whenDo, maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public static T WaitRetryDoUntil<T>(
        this T target,
        Action<T> action,
        Func<T, bool> until,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        Util.TaskRunner.WaitRetryDoUntil(() => action(target), () => until(target), maxWaitSeconds, waitForMsg: waitForMsg);

        return target;
    }

    #region ThenFrom

    public static async Task<ValueTuple<TR1, TR2>> ThenGetAll<TR1, TR2>(
        this Task task,
        Func<TR1> fr1,
        Func<TR2> fr2)
    {
        await task;
        return (fr1(), fr2());
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAll<TR1, TR2, TR3>(
        this Task task,
        Func<TR1> fr1,
        Func<TR2> fr2,
        Func<TR3> fr3)
    {
        await task;
        return (fr1(), fr2(), fr3());
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4>> ThenGetAll<TR1, TR2, TR3, TR4>(
        this Task task,
        Func<TR1> fr1,
        Func<TR2> fr2,
        Func<TR3> fr3,
        Func<TR4> fr4)
    {
        await task;
        return (fr1(), fr2(), fr3(), fr4());
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4, TR5>> ThenGetAll<TR1, TR2, TR3, TR4, TR5>(
        this Task task,
        Func<TR1> fr1,
        Func<TR2> fr2,
        Func<TR3> fr3,
        Func<TR4> fr4,
        Func<TR5> fr5)
    {
        await task;
        return (fr1(), fr2(), fr3(), fr4(), fr5());
    }


    public static async Task<ValueTuple<TR1, TR2>> ThenGetAll<T, TR1, TR2>(
        this Task<T> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2)
    {
        var tResult = await task;
        return (fr1(tResult), fr2(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAll<T, TR1, TR2, TR3>(
        this Task<T> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3)
    {
        var tResult = await task;
        return (fr1(tResult), fr2(tResult), fr3(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4>> ThenGetAll<T, TR1, TR2, TR3, TR4>(
        this Task<T> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3,
        Func<T, TR4> fr4)
    {
        var tResult = await task;
        return (fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4, TR5>> ThenGetAll<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<T> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3,
        Func<T, TR4> fr4,
        Func<T, TR5> fr5)
    {
        var tResult = await task;
        return (fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2>> ThenGetAll<T1, T2, TR1, TR2>(
        this Task<ValueTuple<T1, T2>> task,
        Func<T1, T2, TR1> fr1,
        Func<T1, T2, TR2> fr2)
    {
        var tResult = await task;
        return (fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAll<T1, T2, TR1, TR2, TR3>(
        this Task<ValueTuple<T1, T2>> task,
        Func<T1, T2, TR1> fr1,
        Func<T1, T2, TR2> fr2,
        Func<T1, T2, TR3> fr3)
    {
        var tResult = await task;
        return (fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2), fr3(tResult.Item1, tResult.Item2));
    }

    public static async Task<ValueTuple<TR1, TR2>> ThenGetAll<T1, T2, T3, TR1, TR2>(
        this Task<ValueTuple<T1, T2, T3>> task,
        Func<T1, T2, T3, TR1> fr1,
        Func<T1, T2, T3, TR2> fr2)
    {
        var tResult = await task;
        return (fr1(tResult.Item1, tResult.Item2, tResult.Item3), fr2(tResult.Item1, tResult.Item2, tResult.Item3));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAll<T1, T2, T3, TR1, TR2, TR3>(
        this Task<ValueTuple<T1, T2, T3>> task,
        Func<T1, T2, T3, TR1> fr1,
        Func<T1, T2, T3, TR2> fr2,
        Func<T1, T2, T3, TR3> fr3)
    {
        var tResult = await task;
        return (fr1(tResult.Item1, tResult.Item2, tResult.Item3), fr2(tResult.Item1, tResult.Item2, tResult.Item3), fr3(tResult.Item1, tResult.Item2, tResult.Item3));
    }


    public static async Task<ValueTuple<TR1, TR2>> ThenGetAllAsync<TR1, TR2>(
        this Task task,
        Func<Task<TR1>> fr1,
        Func<Task<TR2>> fr2)
    {
        await task;

        return await Util.TaskRunner.WhenAll(fr1(), fr2());
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAllAsync<TR1, TR2, TR3>(
        this Task task,
        Func<Task<TR1>> fr1,
        Func<Task<TR2>> fr2,
        Func<Task<TR3>> fr3)
    {
        await task;
        return await Util.TaskRunner.WhenAll(fr1(), fr2(), fr3());
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4>> ThenGetAllAsync<TR1, TR2, TR3, TR4>(
        this Task task,
        Func<Task<TR1>> fr1,
        Func<Task<TR2>> fr2,
        Func<Task<TR3>> fr3,
        Func<Task<TR4>> fr4)
    {
        await task;
        return await Util.TaskRunner.WhenAll(fr1(), fr2(), fr3(), fr4());
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4, TR5>> ThenGetAllAsync<TR1, TR2, TR3, TR4, TR5>(
        this Task task,
        Func<Task<TR1>> fr1,
        Func<Task<TR2>> fr2,
        Func<Task<TR3>> fr3,
        Func<Task<TR4>> fr4,
        Func<Task<TR5>> fr5)
    {
        await task;
        return await Util.TaskRunner.WhenAll(fr1(), fr2(), fr3(), fr4(), fr5());
    }

    public static async Task<ValueTuple<TR1, TR2>> ThenGetAllAsync<T, TR1, TR2>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAllAsync<T, TR1, TR2, TR3>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult), fr3(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4>> ThenGetAllAsync<T, TR1, TR2, TR3, TR4>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3, TR4, TR5>> ThenGetAllAsync<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4,
        Func<T, Task<TR5>> fr5)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult));
    }


    public static async Task<ValueTuple<TR1, TR2>> ThenGetAllAsync<T1, T2, TR1, TR2>(
        this Task<ValueTuple<T1, T2>> task,
        Func<T1, T2, Task<TR1>> fr1,
        Func<T1, T2, Task<TR2>> fr2)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAllAsync<T1, T2, TR1, TR2, TR3>(
        this Task<ValueTuple<T1, T2>> task,
        Func<T1, T2, Task<TR1>> fr1,
        Func<T1, T2, Task<TR2>> fr2,
        Func<T1, T2, Task<TR3>> fr3)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2), fr3(tResult.Item1, tResult.Item2));
    }

    public static async Task<ValueTuple<TR1, TR2>> ThenGetAllAsync<T1, T2, T3, TR1, TR2>(
        this Task<ValueTuple<T1, T2, T3>> task,
        Func<T1, T2, T3, Task<TR1>> fr1,
        Func<T1, T2, T3, Task<TR2>> fr2)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(fr1(tResult.Item1, tResult.Item2, tResult.Item3), fr2(tResult.Item1, tResult.Item2, tResult.Item3));
    }

    public static async Task<ValueTuple<TR1, TR2, TR3>> ThenGetAllAsync<T1, T2, T3, TR1, TR2, TR3>(
        this Task<ValueTuple<T1, T2, T3>> task,
        Func<T1, T2, T3, Task<TR1>> fr1,
        Func<T1, T2, T3, Task<TR2>> fr2,
        Func<T1, T2, T3, Task<TR3>> fr3)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(
            fr1(tResult.Item1, tResult.Item2, tResult.Item3),
            fr2(tResult.Item1, tResult.Item2, tResult.Item3),
            fr3(tResult.Item1, tResult.Item2, tResult.Item3));
    }


    public static async Task<ValueTuple<T, TR1, TR2>> ThenWithAllAsync<T, TR1, TR2>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult));
    }

    public static async Task<ValueTuple<T, TR1, TR2, TR3>> ThenWithAllAsync<T, TR1, TR2, TR3>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult), fr3(tResult));
    }

    public static async Task<ValueTuple<T, TR1, TR2, TR3, TR4>> ThenWithAllAsync<T, TR1, TR2, TR3, TR4>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult));
    }

    public static async Task<ValueTuple<T, TR1, TR2, TR3, TR4, TR5>> ThenWithAllAsync<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4,
        Func<T, Task<TR5>> fr5)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult));
    }

    #endregion
}
