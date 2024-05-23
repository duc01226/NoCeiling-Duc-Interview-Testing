using System.Runtime.CompilerServices;
using Easy.Platform.Common.Extensions.WhenCases;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Extensions;

public static class TaskExtension
{
    /// <summary>
    /// Transforms the result of a task using a provided function.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <typeparam name="TR">The type of the result produced by the function.</typeparam>
    /// <param name="task">The task to be transformed.</param>
    /// <param name="f">The function to apply to the result of the task.</param>
    /// <returns>A task that represents the transformed result.</returns>
    /// <remarks>
    /// This method allows for chaining of asynchronous operations without the need for nested callbacks or explicit continuation tasks.
    /// </remarks>
    public static async Task<TR> Then<T, TR>(
        this Task<T> task,
        Func<T, TR> f)
    {
        return f(await task);
    }

    /// <summary>
    /// Executes a function on the result of a Task once it has completed.
    /// </summary>
    /// <typeparam name="T">The type of the result of the Task.</typeparam>
    /// <param name="task">The Task to execute the function on.</param>
    /// <param name="f">The function to execute on the Task's result.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public static async Task Then<T>(
        this Task<T> task,
        Func<T, Task> f)
    {
        await f(await task);
    }

    /// <summary>
    /// </summary>
    /// <typeparam name="TR">The type of the result of the function.</typeparam>
    /// <param name="task">The original task.</param>
    /// <param name="f">A function that transforms the result of the original task into a new result.</param>
    /// <returns>A new task that represents the transformation of the original task by the function.</returns>
    public static async Task<TR> Then<TR>(
        this Task task,
        Func<TR> f)
    {
        await task;
        return f();
    }

    /// <summary>
    /// Executes the provided action after the completion of the given task.
    /// </summary>
    /// <param name="task">The task to await.</param>
    /// <param name="f">The action to execute after the task completes.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task ThenAction(
        this Task task,
        Action f)
    {
        await task;
        f();
    }

    /// <summary>
    /// Asynchronously waits for the task to complete, and then executes the provided function.
    /// </summary>
    /// <param name="task">The task to await.</param>
    /// <param name="f">The function to execute after the task completes.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public static async Task ThenAction(
        this Task task,
        Func<Task> f)
    {
        await task;
        await f();
    }

    /// <summary>
    /// Transforms the result of a task with a specified function after the task has completed.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the antecedent <see cref="Task{TResult}" />.</typeparam>
    /// <typeparam name="TR">The type of the result produced by the transformation function.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="nextTask">A function to apply to the result of the task when it completes.</param>
    /// <returns>A new task that will complete with the result of the function.</returns>
    /// <remarks>
    /// This method allows you to easily chain asynchronous operations without having to nest callbacks.
    /// It is based on the idea of "continuations" in functional programming.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var task = GetSomeTask();
    /// var transformedTask = task.Then(result => TransformResult(result));
    /// </code>
    /// </example>
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

    public static async Task<T> ThenAction<T>(
        this Task<T> task,
        Action<T> action)
    {
        var targetValue = await task;

        action(targetValue);

        return targetValue;
    }

    /// <summary>
    /// Executes the provided task and then performs the specified action asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="task">The task to be executed.</param>
    /// <param name="nextTask">The action to be performed after the task execution.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the original task.</returns>
    public static async Task<T> ThenActionAsync<T>(
        this Task<T> task,
        Func<T, Task> nextTask)
    {
        var targetValue = await task;

        await nextTask(targetValue);

        return targetValue;
    }

    /// <summary>
    /// Executes a specified action on the result of the Task if a given condition is true.
    /// </summary>
    /// <typeparam name="T">The type of result produced by the Task.</typeparam>
    /// <param name="task">The Task on which the action is to be performed.</param>
    /// <param name="actionIf">A boolean value that determines whether the action should be performed.</param>
    /// <param name="action">The action to be performed on the result of the Task if the condition is true.</param>
    /// <returns>The original result of the Task.</returns>
    public static async Task<T> ThenActionIf<T>(
        this Task<T> task,
        bool actionIf,
        Action<T> action)
    {
        var targetValue = await task;

        if (actionIf) action(targetValue);

        return targetValue;
    }

    /// <summary>
    /// Executes a given task and then performs another task if a specified condition is true.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="task">The task to be executed.</param>
    /// <param name="actionIf">The condition that determines whether the next task should be executed.</param>
    /// <param name="nextTask">The task to be executed if the condition is true.</param>
    /// <returns>The result of the original task.</returns>
    public static async Task<T> ThenActionIfAsync<T>(
        this Task<T> task,
        bool actionIf,
        Func<T, Task> nextTask)
    {
        var targetValue = await task;

        if (actionIf) await nextTask(targetValue);

        return targetValue;
    }

    /// <summary>
    /// Executes a side effect action on the result of the given task. If the side effect action throws an exception, it logs the error and continues with the original task result.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <param name="task">The task on which to perform the side effect action.</param>
    /// <param name="action">The side effect action to perform on the result of the task.</param>
    /// <param name="logger">The logger used to log any exceptions thrown by the side effect action.</param>
    /// <returns>The original task result, regardless of whether the side effect action succeeded or failed.</returns>
    public static async Task<T> ThenSideEffectAction<T>(
        this Task<T> task,
        Action<T> action,
        ILogger logger = null)
    {
        var targetValue = await task;

        try
        {
            action(targetValue);
        }
        catch (Exception e)
        {
            logger?.LogError(e.BeautifyStackTrace(), "SideEffectAction failed");
        }

        return targetValue;
    }

    /// <summary>
    /// Executes the given task and then performs a side effect action asynchronously. If the side effect action fails, the exception is logged.
    /// </summary>
    /// <param name="task">The task to be executed.</param>
    /// <param name="nextTask">The side effect action to be performed after the task execution.</param>
    /// <param name="logger">The logger used to log any exceptions that occur during the execution of the side effect action.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public static async Task ThenSideEffectActionAsync(
        this Task task,
        Func<Task> nextTask,
        ILogger logger = null)
    {
        await task;

        try
        {
            await nextTask();
        }
        catch (Exception e)
        {
            logger?.LogError(e.BeautifyStackTrace(), "SideEffectAction failed");
        }
    }

    /// <summary>
    /// Executes a side effect action asynchronously after the completion of the given task.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="task">The task after which the side effect action is to be executed.</param>
    /// <param name="nextTask">The side effect action to be executed after the task. This action takes the result of the task as a parameter.</param>
    /// <param name="logger">The logger to be used for logging any exceptions that occur during the execution of the side effect action.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the original task.</returns>
    /// <remarks>
    /// The side effect action is executed regardless of whether the original task completed successfully or faulted.
    /// If the side effect action throws an exception, this exception is logged and ignored, and does not affect the result of the returned task.
    /// </remarks>
    public static async Task<T> ThenSideEffectActionAsync<T>(
        this Task<T> task,
        Func<T, Task> nextTask,
        ILogger logger = null)
    {
        var targetValue = await task;

        try
        {
            await nextTask(targetValue);
        }
        catch (Exception e)
        {
            logger?.LogError(e.BeautifyStackTrace(), "SideEffectAction failed");
        }

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

    /// <summary>
    /// Executes the specified asynchronous task if the provided condition is met.
    /// </summary>
    /// <typeparam name="TTarget">The type of the result of the task.</typeparam>
    /// <typeparam name="TResult">The type of the result of the next task.</typeparam>
    /// <param name="task">The task to be executed.</param>
    /// <param name="if">A function that defines the condition to be met for the next task to be executed.</param>
    /// <param name="nextTask">The next task to be executed if the condition is met.</param>
    /// <returns>The result of the next task if the condition is met; otherwise, the result of the original task.</returns>
    public static async Task<TResult> ThenIf<TTarget, TResult>(
        this Task<TTarget> task,
        Func<TTarget, bool> @if,
        Func<TTarget, Task<TResult>> nextTask) where TTarget : TResult
    {
        var targetValue = await task;
        return @if(targetValue) ? await nextTask(targetValue) : targetValue;
    }

    /// <summary>
    /// Executes the provided function if the condition is met, otherwise returns a default value.
    /// </summary>
    /// <typeparam name="TTarget">The type of the result of the task.</typeparam>
    /// <typeparam name="TResult">The type of the result of the function or the default value.</typeparam>
    /// <param name="task">The task to be processed.</param>
    /// <param name="if">A function that defines the condition to be met.</param>
    /// <param name="nextTask">The function to be executed if the condition is met.</param>
    /// <param name="defaultValue">The default value to be returned if the condition is not met.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the function if the condition is met, otherwise the default value.</returns>
    public static async Task<TResult> ThenIfOrDefault<TTarget, TResult>(
        this Task<TTarget> task,
        Func<TTarget, bool> @if,
        Func<TTarget, Task<TResult>> nextTask,
        TResult defaultValue = default)
    {
        var targetValue = await task;
        return @if(targetValue) ? await nextTask(targetValue) : defaultValue;
    }

    /// <summary>
    /// Asynchronously executes a function on the result of a task and returns a tuple containing the original task result and the result of the function.
    /// </summary>
    /// <typeparam name="T">The result type of the task.</typeparam>
    /// <typeparam name="T1">The result type of the function.</typeparam>
    /// <param name="task">The task to execute the function on.</param>
    /// <param name="getWith">The function to execute on the task result.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is a tuple containing the original task result and the result of the function.</returns>
    public static Task<ValueTuple<T, T1>> ThenGetWith<T, T1>(this Task<T> task, Func<T, T1> getWith)
    {
        return task.Then(p => (p, getWith(p)));
    }

    /// <summary>
    /// Asynchronously executes a function on the result of a task and returns a tuple containing the original task result and the result of the function.
    /// </summary>
    /// <typeparam name="T">The type of the result of the original task.</typeparam>
    /// <typeparam name="T1">The type of the result of the function.</typeparam>
    /// <param name="task">The original task.</param>
    /// <param name="getWith">The function to execute on the result of the original task.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is a tuple containing the original task result and the result of the function.</returns>
    public static async Task<ValueTuple<T, T1>> ThenGetWith<T, T1>(this Task<T> task, Func<T, Task<T1>> getWith)
    {
        var value = await task;
        var withValue = await getWith(value);
        return (value, withValue);
    }

    /// <summary>
    /// Asynchronously executes the provided functions after the completion of the target task.
    /// </summary>
    /// <typeparam name="T">The type of the result of the target task.</typeparam>
    /// <typeparam name="T1">The type of the result of the first function.</typeparam>
    /// <typeparam name="T2">The type of the result of the second function.</typeparam>
    /// <param name="task">The target task.</param>
    /// <param name="getWith1">The first function to execute after the task completes.</param>
    /// <param name="getWith2">The second function to execute after the first function completes. It takes the results of the task and the first function as parameters.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with the results of the task, the first function, and the second function.</returns>
    public static async Task<ValueTuple<T, T1, T2>> ThenGetWith<T, T1, T2>(this Task<T> task, Func<T, T1> getWith1, Func<T, T1, T2> getWith2)
    {
        var (value, value1) = await task.ThenGetWith(getWith1);
        return (value, value1, getWith2(value, value1));
    }

    /// <summary>
    /// Asynchronously executes a task and then applies two functions to the result of the task.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <typeparam name="T1">The type of the result of the first function.</typeparam>
    /// <typeparam name="T2">The type of the result of the second function.</typeparam>
    /// <param name="task">The task to execute.</param>
    /// <param name="getWith1">The first function to apply to the result of the task. This function should return a Task of type T1.</param>
    /// <param name="getWith2">The second function to apply to the result of the first function and the result of the task. This function should return a Task of type T2.</param>
    /// <returns>A Task that represents the asynchronous operation. The result of the Task is a tuple containing the result of the task, the result of the first function, and the result of the second function.</returns>
    public static async Task<ValueTuple<T, T1, T2>> ThenGetWith<T, T1, T2>(this Task<T> task, Func<T, Task<T1>> getWith1, Func<T, T1, Task<T2>> getWith2)
    {
        var (value, value1) = await task.ThenGetWith(getWith1);
        return (value, value1, await getWith2(value, value1));
    }

    /// <summary>
    /// Use WaitResult to help if exception to see the stack trace. <br />
    /// Task.Wait() will lead to stack trace lost. <br />
    /// Because the stack trace is technically about where the code is returning to, not where the code came from <br />
    /// When you write “await task;”, the compiler translates that into usage of the Task.GetAwaiter() method, <br />
    /// which returns an instance that has a GetResult() method. When used on a faulted Task, GetResult() will propagate the original exception (this is how “await task;” gets its behavior). <br />
    /// You can thus use “task.GetAwaiter().GetResult()” if you want to directly invoke this propagation logic.
    /// </summary>
    public static void WaitResult(this Task task)
    {
        task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Use WaitResult to help if exception to see the stack trace. <br />
    /// Task.Wait() will lead to stack trace lost. <br />
    /// Because the stack trace is technically about where the code is returning to, not where the code came from <br />
    /// When you write “await task;”, the compiler translates that into usage of the Task.GetAwaiter() method, <br />
    /// which returns an instance that has a GetResult() method. When used on a faulted Task, GetResult() will propagate the original exception (this is how “await task;” gets its behavior). <br />
    /// You can thus use “task.GetAwaiter().GetResult()” if you want to directly invoke this propagation logic.
    /// </summary>
    public static T GetResult<T>(this Task<T> task)
    {
        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Provides a fallback mechanism for a task in case of failure.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="task">The task to recover from if it faults.</param>
    /// <param name="fallback">A function that takes the exception thrown by the task and returns a fallback value of type T.</param>
    /// <returns>A new task that will complete with the result of the original task if it succeeds, or with the result of the fallback function if the original task faults.</returns>
    public static Task<T> Recover<T>(
        this Task<T> task,
        Func<Exception, T> fallback)
    {
        return task.ContinueWith(
            t => t.Status == TaskStatus.Faulted
                ? fallback(t.Exception)
                : t.GetResult());
    }

    /// <summary>
    /// Wraps the specified value into a Task.
    /// </summary>
    /// <typeparam name="T">The type of the value to be wrapped.</typeparam>
    /// <param name="t">The value to be wrapped.</param>
    /// <returns>A Task containing the specified value.</returns>
    public static Task<T> BoxedInTask<T>(this T t)
    {
        return Task.FromResult(t);
    }

    /// <summary>
    /// Suspends the current thread for a specified time.
    /// </summary>
    /// <param name="target">The target object of type T.</param>
    /// <param name="maxWaitSeconds">The maximum amount of time to wait in seconds.</param>
    /// <returns>Returns the target object of type T.</returns>
    /// <remarks>
    /// This method uses the `Util.TaskRunner.Wait` method to suspend the current thread.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var result = someObject.Wait(5);
    /// </code>
    /// </example>
    /// <exception cref="ThreadInterruptedException">
    /// The thread is interrupted while waiting.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value of maxWaitSeconds is negative and not equal to -1 milliseconds, or is greater than
    /// <see cref="int.MaxValue" /> milliseconds.
    /// </exception>
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

    public static Task<TResult> WaitUntilGetValidResultAsync<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultWaitIntervalSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetValidResultAsync(target, getResult, condition, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static Task<TResult> WaitUntilGetValidResultAsync<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Action<T> continueWaitOnlyWhen,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetValidResultAsync(target, getResult, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
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

    public static Task<TResult> WaitUntilGetValidResultAsync<T, TResult, TAny>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetValidResultAsync(target, getResult, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    /// <summary>
    /// WaitUntilGetValidResult. If failed return default value.
    /// </summary>
    public static Task<TResult> TryWaitUntilGetValidResultAsync<T, TResult, TAny>(
        this T target,
        Func<T, TResult> getResult,
        Func<TResult, bool> condition,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        try
        {
            return Util.TaskRunner.WaitUntilGetValidResultAsync(target, getResult, condition, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// Waits until the result of the specified function is not null.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="target">The target object.</param>
    /// <param name="getResult">The function to get the result.</param>
    /// <param name="maxWaitSeconds">The maximum number of seconds to wait. Default is 60 seconds.</param>
    /// <param name="delayRetryTimeSeconds">delayRetryTimeSeconds</param>
    /// <param name="waitForMsg">The message to display while waiting. Default is null.</param>
    /// <returns>The result of the function when it's not null.</returns>
    public static Task<TResult> WaitUntilNotNullAsync<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        double delayRetryTimeSeconds = Util.TaskRunner.DefaultWaitIntervalSeconds,
        string waitForMsg = null)
    {
        return WaitUntilGetValidResultAsync(target, getResult, _ => _ != null, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
    }

    public static Task<TResult> WaitUntilGetSuccessAsync<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccessAsync(target, getResult, maxWaitSeconds, waitForMsg);
    }

    public static Task<TResult> WaitUntilGetSuccessAsync<T, TResult, TAny>(
        this T target,
        Func<T, TResult> getResult,
        Func<T, TAny> continueWaitOnlyWhen = null,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccessAsync(target, getResult, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
    }

    public static Task<TResult> WaitUntilGetSuccessAsync<T, TResult>(
        this T target,
        Func<T, TResult> getResult,
        Action<T> continueWaitOnlyWhen,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccessAsync(target, getResult, continueWaitOnlyWhen, maxWaitSeconds, waitForMsg);
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
        await Util.TaskRunner.WaitUntilToDo(() => condition(target), action, maxWaitSeconds, waitForMsg: waitForMsg);

        return target;
    }

    public static Task<TResult> WaitUntilToDo<T, TResult>(
        this T target,
        Func<T, bool> condition,
        Func<T, Task<TResult>> action,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return Util.TaskRunner.WaitUntilToDo(() => condition(target), () => action(target), maxWaitSeconds, waitForMsg: waitForMsg);
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
        return source.WaitUntilHasMatchedCase(_ => whenDo, maxWaitSeconds, waitForMsg);
    }

    public static TSource WaitUntilHasMatchedCase<TSource>(
        this TSource source,
        WhenCase<TSource> whenDo,
        double maxWaitSeconds = Util.TaskRunner.DefaultWaitUntilMaxSeconds,
        string waitForMsg = null)
    {
        return source.WaitUntilHasMatchedCase(_ => whenDo, maxWaitSeconds, waitForMsg);
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

    public static async Task Timeout(this Task task, TimeSpan maxTimeout)
    {
        await task.Then(() => ValueTuple.Create()).Timeout(maxTimeout);
    }

    public static async Task<T> Timeout<T>(this Task<T> task, TimeSpan maxTimeout)
    {
        var timeoutTaskCts = new CancellationTokenSource();

        var timeoutTask = Task.Delay(maxTimeout, timeoutTaskCts.Token);

        var finalTask = await Task.WhenAny(
            task,
            timeoutTask);

        if (finalTask == timeoutTask) throw new Exception($"Task timed out. MaxTimeout: {maxTimeout.Milliseconds} milliseconds");

        await timeoutTaskCts.CancelAsync();
        timeoutTaskCts.Dispose();

        return await task;
    }

    public static TResult ExecuteUseThreadLock<TTarget, TResult>(this TTarget target, object lockObj, Func<TTarget, TResult> fn)
    {
        lock (lockObj)
        {
            return fn(target);
        }
    }

    public static void ExecuteUseThreadLock<TTarget>(this TTarget target, object lockObj, Action<TTarget> fn)
    {
        lock (lockObj)
        {
            fn(target);
        }
    }

    public static TResult ExecuteUseThreadLockAsync<TTarget, TResult>(this TTarget target, object lockObj, Func<TTarget, Task<TResult>> fn)
    {
        lock (lockObj)
        {
            return fn(target).GetResult();
        }
    }

    public static void ExecuteUseThreadLockAsync<TTarget>(this TTarget target, object lockObj, Func<TTarget, Task> fn)
    {
        lock (lockObj)
        {
            fn(target).Wait();
        }
    }

    #region Tuple await all

    public static TaskAwaiter<(T1, T2)> GetAwaiter<T1, T2>(this (Task<T1>, Task<T2>) tasksTuple)
    {
        return CombineTasks().GetAwaiter();

        async Task<(T1, T2)> CombineTasks()
        {
            await Task.WhenAll(tasksTuple.Item1, tasksTuple.Item2);

            return (tasksTuple.Item1.Result, tasksTuple.Item2.Result);
        }
    }

    public static TaskAwaiter<(T1, T2, T3)> GetAwaiter<T1, T2, T3>(this (Task<T1>, Task<T2>, Task<T3>) tasksTuple)
    {
        return CombineTasks().GetAwaiter();

        async Task<(T1, T2, T3)> CombineTasks()
        {
            await Task.WhenAll(tasksTuple.Item1, tasksTuple.Item2, tasksTuple.Item3);

            return (tasksTuple.Item1.Result, tasksTuple.Item2.Result, tasksTuple.Item3.Result);
        }
    }

    public static TaskAwaiter<(T1, T2, T3, T4)> GetAwaiter<T1, T2, T3, T4>(this (Task<T1>, Task<T2>, Task<T3>, Task<T4>) tasksTuple)
    {
        return CombineTasks().GetAwaiter();

        async Task<(T1, T2, T3, T4)> CombineTasks()
        {
            await Task.WhenAll(tasksTuple.Item1, tasksTuple.Item2, tasksTuple.Item3, tasksTuple.Item4);

            return (tasksTuple.Item1.Result, tasksTuple.Item2.Result, tasksTuple.Item3.Result, tasksTuple.Item4.Result);
        }
    }

    public static TaskAwaiter<(T1, T2, T3, T4, T5)> GetAwaiter<T1, T2, T3, T4, T5>(this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>) tasksTuple)
    {
        return CombineTasks().GetAwaiter();

        async Task<(T1, T2, T3, T4, T5)> CombineTasks()
        {
            await Task.WhenAll(tasksTuple.Item1, tasksTuple.Item2, tasksTuple.Item3, tasksTuple.Item4, tasksTuple.Item5);

            return (tasksTuple.Item1.Result, tasksTuple.Item2.Result, tasksTuple.Item3.Result, tasksTuple.Item4.Result, tasksTuple.Item5.Result);
        }
    }

    public static TaskAwaiter<(T1, T2, T3, T4, T5, T6)> GetAwaiter<T1, T2, T3, T4, T5, T6>(this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>, Task<T6>) tasksTuple)
    {
        return CombineTasks().GetAwaiter();

        async Task<(T1, T2, T3, T4, T5, T6)> CombineTasks()
        {
            await Task.WhenAll(tasksTuple.Item1, tasksTuple.Item2, tasksTuple.Item3, tasksTuple.Item4, tasksTuple.Item5, tasksTuple.Item6);

            return (tasksTuple.Item1.Result, tasksTuple.Item2.Result, tasksTuple.Item3.Result, tasksTuple.Item4.Result, tasksTuple.Item5.Result, tasksTuple.Item6.Result);
        }
    }

    public static TaskAwaiter<List<T>> GetAwaiter<T>(this IEnumerable<Task<T>> tasks)
    {
        return Util.TaskRunner.WhenAll(tasks).GetAwaiter();
    }

    public static Task<List<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks)
    {
        return Util.TaskRunner.WhenAll(tasks);
    }

    #endregion

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
        return await Util.TaskRunner.WhenAll(tResult.BoxedInTask(), fr1(tResult), fr2(tResult));
    }

    public static async Task<ValueTuple<T, TR1, TR2, TR3>> ThenWithAllAsync<T, TR1, TR2, TR3>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(tResult.BoxedInTask(), fr1(tResult), fr2(tResult), fr3(tResult));
    }

    public static async Task<ValueTuple<T, TR1, TR2, TR3, TR4>> ThenWithAllAsync<T, TR1, TR2, TR3, TR4>(
        this Task<T> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4)
    {
        var tResult = await task;
        return await Util.TaskRunner.WhenAll(tResult.BoxedInTask(), fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult));
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
        return await Util.TaskRunner.WhenAll(tResult.BoxedInTask(), fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult));
    }

    #endregion
}
