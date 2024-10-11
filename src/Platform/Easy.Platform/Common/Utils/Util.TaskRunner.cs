using System.Collections.Concurrent;
using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Extensions.WhenCases;
using Easy.Platform.Common.Logging;
using Microsoft.Extensions.Logging;
using Polly;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class TaskRunner
    {
        public const int DefaultWaitUntilMaxSeconds = 60;
        public const double DefaultWaitIntervalSeconds = 2;
        public const int DefaultResilientRetryCount = 3;

        /// <summary>
        /// A typical recommendation is to use a factor of 10-50 times the number of processor cores for I/O-bound tasks.
        /// Example: Assume: 50 ms average latency for PostgreSQL queries; 70 ms average latency for MongoDB queries; 10 ms CPU processing time per query;
        /// Total time per I/O task (I/O + CPU): 50 ms (I/O) + 10 ms (CPU) = 60 ms
        /// Max parallel tasks per core: PostgreSQL: 60ms/10ms(CPU processing time) = 6 tasks/core; MongoDB: 80ms/10ms = 8 tasks/core
        /// </summary>
        public static int DefaultNumberOfParallelIoTasksPerCpuRatio { get; set; } = 10;

        public static int DefaultNumberOfParallelComputeTasksPerCpuRatio { get; set; } = 2;

        public static int DefaultParallelIoTaskMaxConcurrent { get; set; } =
            (Environment.ProcessorCount * DefaultNumberOfParallelIoTasksPerCpuRatio)
            .PipeIf(PlatformEnvironment.IsDevelopment, p => Math.Min(p, DefaultNumberOfParallelIoTasksPerCpuRatio * 2));

        public static int DefaultParallelComputeTaskMaxConcurrent { get; set; } =
            (Environment.ProcessorCount * DefaultNumberOfParallelComputeTasksPerCpuRatio)
            .PipeIf(PlatformEnvironment.IsDevelopment, p => Math.Min(p, DefaultNumberOfParallelComputeTasksPerCpuRatio * 2));

        /// <summary>
        /// Execute an action after a given of time.
        /// </summary>
        public static async Task QueueDelayAsyncAction(
            Func<CancellationToken, Task> action,
            TimeSpan delayTime,
            CancellationToken cancellationToken = default)
        {
            if (delayTime > TimeSpan.Zero)
                await Task.Delay(delayTime, cancellationToken);
            await action(cancellationToken);
        }

        /// <summary>
        /// Execute an action after a given of time.
        /// </summary>
        public static async Task<TResult> QueueDelayAsyncAction<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            TimeSpan delayTime,
            CancellationToken cancellationToken = default)
        {
            if (delayTime > TimeSpan.Zero)
                await Task.Delay(delayTime, cancellationToken);
            return await action(cancellationToken);
        }

        /// <summary>
        /// Execute an action after a given of time.
        /// </summary>
        public static async Task QueueDelayAction(
            Action action,
            TimeSpan delayTime,
            CancellationToken cancellationToken = default)
        {
            if (delayTime > TimeSpan.Zero)
                await Task.Delay(delayTime, cancellationToken);
            action();
        }

        /// <summary>
        /// Queues a specified action to be executed in the background.
        /// </summary>
        /// <param name="action">The action to be executed in the background. This is a task-returning asynchronous method.</param>
        /// <param name="loggerFactory">A factory method that creates an ILogger instance.</param>
        /// <param name="delayTimeSeconds">The delay time in seconds before the action is executed. Default is 0, which means the action is executed immediately.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the background task. Default is an empty CancellationToken.</param>
        /// <remarks>
        /// This method uses Task.Run to execute the action in the background.
        /// It also captures the current stack trace before the Task.Run call,
        /// because the stack trace gets lost after the action starts executing.
        /// If the action throws an exception, it is caught and logged using the ILogger instance created by the loggerFactory.
        /// </remarks>
        public static void QueueActionInBackground(
            Func<Task> action,
            Func<ILogger> loggerFactory,
            int delayTimeSeconds = 0,
            CancellationToken cancellationToken = default,
            bool logFullStackTraceBeforeBackgroundTask = true)
        {
            // Must use stack trace BEFORE Task.Run to run some new action in background. BECAUSE after call get data function, the stack trace get lost, only back to task.run.
            var fullStackTrace = logFullStackTraceBeforeBackgroundTask ? PlatformEnvironment.StackTrace() : null;

            Task.Run(
                async () =>
                {
                    PlatformLogger.BackgroundThreadFullStackTraceContextAccessor.Current = fullStackTrace;

                    try
                    {
                        await QueueDelayAsyncAction(_ => action(), delayTimeSeconds.Seconds(), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException) return;

                        loggerFactory().LogError(ex.BeautifyStackTrace(), "Run in background thread failed.");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Queues a task that returns a result to run in the background.
        /// </summary>
        /// <param name="action">The task that returns a result to be executed.</param>
        /// <param name="loggerFactory">The factory method to create an ILogger instance.</param>
        /// <param name="delayTimeSeconds">The delay time in seconds before the task is executed. Default is 0.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to cancel the task. Default is CancellationToken.None.</param>
        /// <remarks>
        /// This method captures the stack trace before the task is run. If an exception occurs during the execution of the task, it logs an error message with the stack trace.
        /// </remarks>
        /// <typeparam name="TResult">The type of the result returned by the task.</typeparam>
        public static void QueueActionInBackground<TResult>(
            Func<Task<TResult>> action,
            Func<ILogger> loggerFactory,
            int delayTimeSeconds = 0,
            CancellationToken cancellationToken = default,
            bool logFullStackTraceBeforeBackgroundTask = true)
        {
            // Must use stack trace BEFORE Task.Run to run some new action in background. BECAUSE after call get data function, the stack trace get lost, only back to task.run.
            var fullStackTrace = logFullStackTraceBeforeBackgroundTask ? PlatformEnvironment.StackTrace() : null;

            Task.Run(
                async () =>
                {
                    PlatformLogger.BackgroundThreadFullStackTraceContextAccessor.Current = fullStackTrace;

                    try
                    {
                        await QueueDelayAsyncAction(_ => action(), delayTimeSeconds.Seconds(), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException) return;

                        loggerFactory().LogError(ex.BeautifyStackTrace(), "Run in background thread failed.");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Queues the specified action to be executed in the background.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="loggerFactory">A factory function that returns an ILogger instance.</param>
        /// <param name="delayTimeSeconds">The delay time in seconds before the action is executed. Defaults to 0.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the task. Defaults to an empty CancellationToken.</param>
        /// <remarks>
        /// This method captures the current stack trace before the Task.Run call to ensure that the stack trace is available in the background task.
        /// If the action throws an exception, it is logged as an error using the ILogger instance from the loggerFactory.
        /// </remarks>
        public static void QueueActionInBackground(
            Action action,
            Func<ILogger> loggerFactory,
            int delayTimeSeconds = 0,
            CancellationToken cancellationToken = default,
            bool logFullStackTraceBeforeBackgroundTask = true)
        {
            // Must use stack trace BEFORE Task.Run to run some new action in background. BECAUSE after call get data function, the stack trace get lost, only back to task.run.
            var fullStackTrace = logFullStackTraceBeforeBackgroundTask ? PlatformEnvironment.StackTrace() : null;

            Task.Run(
                async () =>
                {
                    PlatformLogger.BackgroundThreadFullStackTraceContextAccessor.Current = fullStackTrace;

                    try
                    {
                        await QueueDelayAction(action, delayTimeSeconds.Seconds(), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException) return;

                        loggerFactory().LogError(ex.BeautifyStackTrace(), "Run in background thread failed.");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Queues an asynchronous action to be executed at regular intervals.
        /// </summary>
        /// <param name="action">The asynchronous action to be executed.</param>
        /// <param name="intervalTimeInSeconds">The time interval in seconds between each execution of the action.</param>
        /// <param name="maximumIntervalExecutionCount">The maximum number of times the action should be executed. If null, the action is executed indefinitely.</param>
        /// <param name="executeOnceImmediately">If set to true, the action is executed once immediately before the interval executions start.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the action execution.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task QueueIntervalAsyncAction(
            Func<CancellationToken, Task> action,
            int intervalTimeInSeconds,
            int? maximumIntervalExecutionCount = null,
            bool executeOnceImmediately = true,
            CancellationToken cancellationToken = default)
        {
            var executionCount = 0;

            if (executeOnceImmediately)
            {
                await action(cancellationToken);
                executionCount += 1;
            }

            if (maximumIntervalExecutionCount <= 0) return;

            while (executionCount < maximumIntervalExecutionCount)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalTimeInSeconds), cancellationToken);
                await action(cancellationToken);
                executionCount += 1;
            }
        }

        /// <summary>
        /// Queues an asynchronous action to be executed in the background at regular intervals.
        /// </summary>
        /// <param name="action">The asynchronous action to be executed.</param>
        /// <param name="intervalTimeInSeconds">The interval time in seconds between each execution of the action.</param>
        /// <param name="loggerFactory">A factory function that creates an ILogger instance.</param>
        /// <param name="maximumIntervalExecutionCount">The maximum number of times the action should be executed. If null, the action is executed indefinitely.</param>
        /// <param name="executeOnceImmediately">If true, the action is executed immediately upon queuing. Otherwise, the action is first executed after the interval time has passed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the background task.</param>
        /// <remarks>
        /// This method is useful for scheduling recurring tasks, such as cache invalidation or data seeding, in a non-blocking manner.
        /// </remarks>
        public static void QueueIntervalAsyncActionInBackground(
            Func<CancellationToken, Task> action,
            int intervalTimeInSeconds,
            Func<ILogger> loggerFactory,
            int? maximumIntervalExecutionCount = null,
            bool executeOnceImmediately = true,
            CancellationToken cancellationToken = default,
            bool logFullStackTraceBeforeBackgroundTask = true)
        {
            // Must use stack trace BEFORE Task.Run to run some new action in background. BECAUSE after call get data function, the stack trace get lost, only back to task.run.
            var fullStackTrace = logFullStackTraceBeforeBackgroundTask ? PlatformEnvironment.StackTrace() : null;

            Task.Run(
                async () =>
                {
                    PlatformLogger.BackgroundThreadFullStackTraceContextAccessor.Current = fullStackTrace;

                    try
                    {
                        await QueueIntervalAsyncAction(action, intervalTimeInSeconds, maximumIntervalExecutionCount, executeOnceImmediately, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException) return;

                        loggerFactory().LogError(ex.BeautifyStackTrace(), "Run in background thread failed.");
                    }
                },
                cancellationToken);
        }

        public static void CatchException(Action action, Action<Exception> onException = null)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                onException?.Invoke(e);
            }
        }

        public static Task CatchException(Func<Task> action, Action<Exception> onException = null)
        {
            return CatchException<Exception>(action, onException);
        }

        public static T CatchException<T>(Func<T> func, Func<Exception, T> onException = null)
        {
            return CatchException<Exception, T>(func, onException);
        }

        public static T CatchException<T>(Func<T> func, T fallbackValue)
        {
            return CatchException<Exception, T>(func, fallbackValue);
        }

        public static T CatchExceptionContinueThrow<T>(Func<T> func, Action<Exception> onException)
        {
            return CatchExceptionContinueThrow<Exception, T>(func, onException);
        }

        public static async Task<T> CatchExceptionContinueThrowAsync<T, TException>(Func<Task<T>> func, Action<TException> onException)
            where TException : Exception
        {
            try
            {
                return await func();
            }
            catch (TException e)
            {
                onException(e);
                throw;
            }
        }

        public static Task<T> CatchExceptionContinueThrowAsync<T>(Func<Task<T>> func, Action<Exception> onException)
        {
            return CatchExceptionContinueThrowAsync<T, Exception>(func, onException);
        }

        public static void CatchExceptionContinueThrow(Action action, Action<Exception> onException)
        {
            CatchExceptionContinueThrow<Exception, object>(action.ToFunc(), onException);
        }

        public static async Task CatchException<TException>(Func<Task> action, Action<TException> onException = null)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException e)
            {
                onException?.Invoke(e);
            }
        }

        public static T CatchException<TException, T>(Func<T> func, Func<TException, T> onException = null)
            where TException : Exception
        {
            try
            {
                return func();
            }
            catch (TException e)
            {
                onException?.Invoke(e);
                return default;
            }
        }

        public static T CatchException<TException, T>(Func<T> func, T fallbackValue)
            where TException : Exception
        {
            try
            {
                return func();
            }
            catch (TException)
            {
                return fallbackValue;
            }
        }

        public static T CatchExceptionContinueThrow<TException, T>(Func<T> func, Action<TException> onException)
            where TException : Exception
        {
            try
            {
                return func();
            }
            catch (TException e)
            {
                onException(e);
                throw;
            }
        }

        public static T CatchExceptionFallBackValue<TException, T>(Func<T> func, Action<TException> onException, T fallbackValue)
            where TException : Exception
        {
            try
            {
                return func();
            }
            catch (TException e)
            {
                onException(e);
                return fallbackValue;
            }
        }

        /// <summary>
        /// Help to profiling an asyncTask. <br />
        /// afterExecution: elapsedMilliseconds => { } is an optional action to execute. It's input is the task ElapsedMilliseconds of asyncTask execution.
        /// </summary>
        public static async Task ProfileExecutionAsync(
            Func<Task> asyncTask,
            Action<double> afterExecution = null,
            Action beforeExecution = null)
        {
            beforeExecution?.Invoke();

            var startTime = Stopwatch.GetTimestamp();

            await asyncTask();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            afterExecution?.Invoke(elapsedTime.TotalMilliseconds);
        }

        /// <summary>
        /// Help to profiling an asyncTask. <br />
        /// afterExecution: (result, elapsedMilliseconds) => { } is an optional action to execute. It's input is the task ElapsedMilliseconds of asyncTask execution.
        /// </summary>
        public static async Task<TResult> ProfileExecutionAsync<TResult>(
            Func<Task<TResult>> asyncTask,
            Action<TResult, double> afterExecution = null,
            Action beforeExecution = null)
        {
            beforeExecution?.Invoke();

            var startTime = Stopwatch.GetTimestamp();

            var result = await asyncTask();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            afterExecution?.Invoke(result, elapsedTime.TotalMilliseconds);

            return result;
        }

        /// <summary>
        /// Help to profiling an action.
        /// afterExecution: elapsedMilliseconds => { } is an optional action to execute. It's input is the task ElapsedMilliseconds of asyncTask execution.
        /// </summary>
        public static void ProfileExecution(
            Action action,
            Action<double> afterExecution = null,
            Action beforeExecution = null)
        {
            beforeExecution?.Invoke();

            var startTime = Stopwatch.GetTimestamp();

            action();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            afterExecution?.Invoke(elapsedTime.TotalMilliseconds);
        }

        /// <summary>
        /// Help to profiling an action.
        /// afterExecution: elapsedMilliseconds => { } is an optional action to execute. It's input is the task ElapsedMilliseconds of asyncTask execution.
        /// </summary>
        public static TResult ProfileExecution<TResult>(
            Func<TResult> action,
            Action<TResult, double> afterExecution = null,
            Action beforeExecution = null)
        {
            beforeExecution?.Invoke();

            var startTime = Stopwatch.GetTimestamp();

            var result = action();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            afterExecution?.Invoke(result, elapsedTime.TotalMilliseconds);

            return result;
        }

        public static Task WhenAll(params Task[] tasks)
        {
            return Task.WhenAll(tasks);
        }

        public static Task WhenAll(IEnumerable<Task> tasks)
        {
            return Task.WhenAll(tasks);
        }

        public static Task<List<T>> WhenAll<T>(IEnumerable<Task<T>> tasks)
        {
            return Task.WhenAll(tasks).Then(i => i.ToList());
        }

        public static Task<List<T>> WhenAll<T>(params Task<T>[] tasks)
        {
            return Task.WhenAll(tasks).Then(i => i.ToList());
        }

        public static async Task<ValueTuple<T1, T2>> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2)
        {
            return (await task1, await task2);
        }

        public static async Task<ValueTuple<T1, T2, T3>> WhenAll<T1, T2, T3>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3)
        {
            return (await task1, await task2, await task3);
        }

        public static async Task<ValueTuple<T1, T2, T3, T4>> WhenAll<T1, T2, T3, T4>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4)
        {
            return (await task1, await task2, await task3, await task4);
        }

        public static async Task<ValueTuple<T1, T2, T3, T4, T5>> WhenAll<T1, T2, T3, T4, T5>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4,
            Task<T5> task5)
        {
            return (await task1, await task2, await task3, await task4, await task5);
        }

        public static async Task<ValueTuple<T1, T2, T3, T4, T5, T6>> WhenAll<T1, T2, T3, T4, T5, T6>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4,
            Task<T5> task5,
            Task<T6> task6)
        {
            return (await task1, await task2, await task3, await task4, await task5, await task6);
        }

        public static Task<T> Async<T>(T t)
        {
            return Task.FromResult(t);
        }

        /// <summary>
        /// WaitRetryThrowFinalExceptionAsync. Throw final exception on max retry reach
        /// </summary>
        /// <typeparam name="TException"></typeparam>
        /// <param name="retryCount"></param>
        /// <param name="sleepDurationProvider">Ex: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))</param>
        /// <param name="executeFunc"></param>
        /// <param name="onBeforeThrowFinalExceptionFn"></param>
        /// <param name="onRetry">onRetry: (exception,timeSpan,currentRetry,context)</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task WaitRetryThrowFinalExceptionAsync<TException>(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null,
            CancellationToken cancellationToken = default) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalExceptionAsync(
                    executeFunc,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }),
                    cancellationToken: cancellationToken);
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context},CancellationToken)" />
        public static Task<T> WaitRetryThrowFinalExceptionAsync<T, TException>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null,
            CancellationToken cancellationToken = default) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => DefaultWaitIntervalSeconds.Seconds()),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalExceptionAsync(
                    executeFunc,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }),
                    cancellationToken: cancellationToken);
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context},CancellationToken)" />
        public static Task WaitRetryThrowFinalExceptionAsync(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            return WaitRetryThrowFinalExceptionAsync<Exception>(
                executeFunc,
                sleepDurationProvider,
                retryCount,
                onBeforeThrowFinalExceptionFn,
                onRetry,
                cancellationToken);
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context},CancellationToken)" />
        public static Task<T> WaitRetryThrowFinalExceptionAsync<T>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            return WaitRetryThrowFinalExceptionAsync<T, Exception>(
                executeFunc,
                sleepDurationProvider,
                retryCount,
                onBeforeThrowFinalExceptionFn,
                onRetry,
                cancellationToken);
        }

        /// <summary>
        /// Executes a task with a retry policy. If the task fails, it will be retried based on the provided retry count and sleep duration.
        /// </summary>
        /// <param name="executeFunc">The function to execute.</param>
        /// <param name="sleepDurationProvider">A function that provides the duration to wait between retries. If not provided, the default wait interval is used.</param>
        /// <param name="retryCount">The number of times to retry the function if it fails. Defaults to 1.</param>
        /// <param name="onRetry">An action to execute after each failed attempt. It provides the exception thrown, the timespan until the next retry, the current retry attempt, and the context.</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>Returns a PolicyResult which captures the result of a policy execution.</returns>
        public static Task<PolicyResult> WaitRetryAsync(
            Func<CancellationToken, Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception, TimeSpan, int, Context> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCaptureAsync(
                    async ct =>
                    {
                        try
                        {
                            await executeFunc(ct);
                        }
                        finally
                        {
                            await GarbageCollector.Collect();
                        }
                    },
                    cancellationToken);
        }

        /// <summary>
        /// Executes a specified asynchronous function with a retry policy.
        /// </summary>
        /// <typeparam name="T">The type of the result produced by the function.</typeparam>
        /// <param name="executeFunc">The function to be executed.</param>
        /// <param name="sleepDurationProvider">A function that provides the duration to wait between retries based on the retry attempt number. If not provided, a default interval is used.</param>
        /// <param name="retryCount">The number of times to retry execution if the function fails. Defaults to 1.</param>
        /// <param name="onRetry">An action to be executed after each failed attempt to execute the function. This action receives the exception thrown, the calculated sleep duration, the current retry attempt number, and the execution context.</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the result of the function execution or the exception thrown if all retries fail.</returns>
        public static Task<PolicyResult<T>> WaitRetryAsync<T>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception, TimeSpan, int, Context> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCaptureAsync(
                    async ct =>
                    {
                        try
                        {
                            return await executeFunc();
                        }
                        finally
                        {
                            await GarbageCollector.Collect();
                        }
                    },
                    cancellationToken);
        }

        /// <summary>
        /// WaitRetryThrowFinalException. Throw final exception on max retry reach
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sleepDurationProvider">Ex: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))</param>
        /// <param name="executeFunc"></param>
        /// <param name="retryCount"></param>
        /// <param name="onBeforeThrowFinalExceptionFn"></param>
        /// <param name="onRetry">onRetry: (exception,timeSpan,currentRetry,context)</param>
        /// <returns></returns>
        public static T WaitRetryThrowFinalException<T>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalException(
                    executeFunc,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        /// <summary>
        /// Executes a function with a retry policy.
        /// </summary>
        /// <typeparam name="T">The type of the result produced by the function.</typeparam>
        /// <param name="executeFunc">The function to execute.</param>
        /// <param name="sleepDurationProvider">A function that provides the duration to wait between retries. If not provided, the default wait interval is used.</param>
        /// <param name="retryCount">The number of times to retry execution if an exception occurs. The default is 1.</param>
        /// <param name="onRetry">An action to execute after each retry. This action receives the exception that caused the retry, the duration to wait before the next retry, the current retry count, and the execution context.</param>
        /// <returns>A PolicyResult object that captures the result of the execution or the exception if one was thrown.</returns>
        public static PolicyResult<T> WaitRetry<T>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(
                    () =>
                    {
                        return executeFunc();
                    });
        }

        /// <summary>
        /// WaitRetryThrowFinalException. Throw final exception on max retry reach
        /// </summary>
        /// <param name="sleepDurationProvider">Ex: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))</param>
        /// <param name="executeAction"></param>
        /// <param name="retryCount"></param>
        /// <param name="onBeforeThrowFinalExceptionFn"></param>
        /// <param name="onRetry">onRetry: (exception,timeSpan,currentRetry,context)</param>
        /// <returns></returns>
        public static void WaitRetryThrowFinalException(
            Action executeAction,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalException(
                    executeAction,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        public static PolicyResult WaitRetry(
            Action executeAction,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(
                    () =>
                    {
                        executeAction();
                    });
        }

        /// <inheritdoc cref="WaitRetryThrowFinalException{T}" />
        public static T WaitRetryThrowFinalException<T, TException>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<TException> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalException(
                    executeFunc,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        /// <summary>
        /// Executes a function with a retry policy. If the function throws an exception of type TException, it will be retried based on the provided parameters.
        /// </summary>
        /// <typeparam name="T">The type of the result returned by the function to execute.</typeparam>
        /// <typeparam name="TException">The type of the exception to handle.</typeparam>
        /// <param name="executeFunc">The function to execute.</param>
        /// <param name="sleepDurationProvider">A function that takes the current retry attempt and returns the duration to wait before the next retry. If not provided, the default wait interval is used.</param>
        /// <param name="retryCount">The number of retry attempts. The default is 1.</param>
        /// <param name="onRetry">An action to execute after each retry attempt. The action takes the exception that caused the retry, the delay before the next retry, the current retry attempt, and the execution context.</param>
        /// <returns>A PolicyResult object containing the result of the function execution or the exception thrown.</returns>
        public static PolicyResult<T> WaitRetry<T, TException>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = DefaultResilientRetryCount,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(
                    () =>
                    {
                        return executeFunc();
                    });
        }

        public static void Wait(int millisecondsToWait)
        {
            Thread.Sleep(millisecondsToWait);
        }

        public static void DoThenWait(Action action, double secondsToWait = DefaultWaitIntervalSeconds)
        {
            action();

            Thread.Sleep((int)(secondsToWait * 1000));
        }

        public static T DoThenWait<T>(Func<T> action, double secondsToWait = DefaultWaitIntervalSeconds)
        {
            var result = action();

            Thread.Sleep((int)(secondsToWait * 1000));

            return result;
        }

        /// <summary>
        /// Waits until the specified condition is met or the maximum wait time is exceeded.
        /// </summary>
        /// <param name="condition">A function that represents the condition to be met.</param>
        /// <param name="maxWaitSeconds">The maximum time in seconds to wait for the condition to be met. The default is <see cref="DefaultWaitUntilMaxSeconds" />.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds to wait between checks of the condition. The default is <see cref="DefaultWaitIntervalSeconds" />.</param>
        /// <param name="waitForMsg">An optional message to be included in the exception if the wait times out.</param>
        /// <exception cref="TimeoutException">Thrown if the condition is not met within the specified maximum wait time.</exception>
        public static void WaitUntil(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
        }

        /// <summary>
        /// Waits until the specified condition is met or a timeout occurs.
        /// </summary>
        /// <param name="condition">The condition to wait for. This is a function that returns a Task of bool.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait in seconds. If this time elapses before the condition is met, a TimeoutException is thrown. Default is 60 seconds.</param>
        /// <param name="waitIntervalSeconds">The interval between condition checks in seconds. Default is 0.3 seconds.</param>
        /// <param name="waitForMsg">An optional message to include in the TimeoutException if the condition is not met within the specified time.</param>
        /// <returns>A Task that completes when the condition is met or the timeout occurs.</returns>
        /// <exception cref="TimeoutException">Thrown if the condition is not met within the specified time.</exception>
        public static async Task WaitUntilAsync(
            Func<Task<bool>> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!await condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    await Task.Delay((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
        }

        /// <summary>
        /// Waits until the specified condition is met or a timeout occurs.
        /// </summary>
        /// <param name="condition">The condition to wait for. This is a function that returns a Task of bool.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait in seconds. If this time elapses before the condition is met, a TimeoutException is thrown. Default is 60 seconds.</param>
        /// <param name="waitIntervalSeconds">The interval between condition checks in seconds. Default is 0.3 seconds.</param>
        /// <param name="waitForMsg">An optional message to include in the TimeoutException if the condition is not met within the specified time.</param>
        /// <returns>A Task that completes when the condition is met or the timeout occurs.</returns>
        /// <exception cref="TimeoutException">Thrown if the condition is not met within the specified time.</exception>
        public static async Task WaitUntilAsync(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    await Task.Delay((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
        }

        /// <summary>
        /// Tries to wait until a specific condition is met.
        /// </summary>
        /// <param name="condition">A function that evaluates the condition to wait for.</param>
        /// <param name="maxWaitSeconds">The maximum time in seconds to wait for the condition to be met. Defaults to <see cref="DefaultWaitUntilMaxSeconds" />.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds to wait between checks of the condition. Defaults to <see cref="DefaultWaitIntervalSeconds" />.</param>
        /// <param name="waitForMsg">An optional message to display while waiting for the condition to be met.</param>
        /// <returns>Returns true if the condition is met within the specified maximum wait time, otherwise returns false.</returns>
        public static bool TryWaitUntil(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            try
            {
                WaitUntil(condition, maxWaitSeconds, waitIntervalSeconds, waitForMsg);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Asynchronously waits until the specified condition is met or until the maximum wait time is exceeded.
        /// </summary>
        /// <param name="condition">The condition to be met, represented as a function that returns a Task of bool.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait in seconds. Default is 60 seconds.</param>
        /// <param name="waitIntervalSeconds">The interval between each check of the condition in seconds. Default is 0.3 seconds.</param>
        /// <param name="waitForMsg">An optional message to be displayed while waiting.</param>
        /// <returns>A Task of bool representing the success of the operation. Returns true if the condition is met within the maximum wait time, otherwise false.</returns>
        /// <exception cref="Exception">Any exceptions thrown within the condition function are caught and result in a return value of false.</exception>
        public static async Task<bool> TryWaitUntilAsync(
            Func<Task<bool>> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            try
            {
                await WaitUntilAsync(condition, maxWaitSeconds, waitIntervalSeconds, waitForMsg);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Waits until the specified condition is met and then returns the result.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="getResult">A function that gets the result from the target object.</param>
        /// <param name="condition">A function that determines whether the result is valid.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for a valid result. The default is <see cref="DefaultWaitUntilMaxSeconds" />.</param>
        /// <param name="delayRetryTimeSeconds">delayRetryTimeSeconds</param>
        /// <param name="waitForMsg">The message to display if the wait times out.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The valid result.</returns>
        /// <exception cref="TimeoutException">Thrown if a valid result is not obtained within the specified maximum wait time.</exception>
        public static async Task<TResult> WaitUntilGetValidResultAsync<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null,
            CancellationToken cancellationToken = default)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;
            var result = getResult(target);

            while (!condition(result))
            {
                if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();

                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                {
                    await Task.Delay((int)(delayRetryTimeSeconds * 1000), cancellationToken);
                    result = getResult(target);
                }
                else
                {
                    throw new TimeoutException(
                        $"WaitUntilGetValidResult is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
                }
            }

            return result;
        }

        /// <summary>
        /// Tries to get a valid result by repeatedly invoking the getResult function until the condition is met or the maximum wait time is exceeded.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="getResult">A function that takes the target object and returns a result.</param>
        /// <param name="condition">A function that takes the result and returns a boolean indicating whether the result is valid.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait for a valid result, in seconds. Default is DefaultWaitUntilMaxSeconds.</param>
        /// <param name="delayRetryTimeSeconds">delayRetryTimeSeconds</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>The valid result, if one is obtained within the maximum wait time; otherwise, the last obtained result.</returns>
        public static async Task<TResult> TryWaitUntilGetValidResultAsync<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultWaitIntervalSeconds,
            CancellationToken cancellationToken = default)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;
            var result = getResult(target);

            while (!condition(result))
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                {
                    await Task.Delay((int)(delayRetryTimeSeconds * 1000), cancellationToken);
                    result = getResult(target);
                }
                else
                {
                    break;
                }

            return result;
        }

        /// <summary>
        /// Tries to get a valid result by repeatedly invoking the getResult function until the condition is met or the maximum wait time is exceeded.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="getResult">A function that takes the target object and returns a result.</param>
        /// <param name="condition">A function that takes the result and returns a boolean indicating whether the result is valid.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait for a valid result, in seconds. Default is DefaultWaitUntilMaxSeconds.</param>
        /// <param name="delayRetryTimeSeconds">delayRetryTimeSeconds</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>The valid result, if one is obtained within the maximum wait time; otherwise, the last obtained result.</returns>
        public static async Task<TResult> TryWaitUntilGetValidResultAsync<T, TResult>(
            T target,
            Func<T, Task<TResult>> getResult,
            Func<TResult, bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultWaitIntervalSeconds,
            CancellationToken cancellationToken = default)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;
            var result = await getResult(target);

            while (!condition(result))
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                {
                    await Task.Delay((int)(delayRetryTimeSeconds * 1000), cancellationToken);
                    result = await getResult(target);
                }
                else
                {
                    break;
                }

            return result;
        }

        /// <summary>
        /// Waits until a specified condition is met.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TAny">The type of the object returned by the continueWaitOnlyWhen function.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="condition">A function that determines the condition to be met.</param>
        /// <param name="continueWaitOnlyWhen">A function that is invoked only when the condition is not met. If this function throws an exception, the wait is stopped immediately.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait. The default is 60 seconds.</param>
        /// <param name="waitForMsg">An optional message to be included in the exception if the wait times out.</param>
        /// <returns>Returns the target object if the condition is met within the specified time, otherwise throws an exception.</returns>
        /// <exception cref="TimeoutException">Thrown when the wait times out before the condition is met.</exception>
        /// <exception cref="Exception">Thrown when the continueWaitOnlyWhen function throws an exception.</exception>
        public static T WaitUntil<T, TAny>(
            T target,
            Func<bool> condition,
            Func<T, TAny> continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            try
            {
                while (!condition())
                {
                    // Retry check condition again to continueWaitOnlyWhen throw error only when condition not matched
                    // Sometime when continueWaitOnlyWhen execute the condition is matched
                    WaitRetryThrowFinalException(
                        () =>
                        {
                            if (!condition()) continueWaitOnlyWhen?.Invoke(target);
                        });

                    if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                        Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));
                    else
                        throw new TimeoutException(
                            $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                            $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
                }

                return target;
            }
            catch (Exception e)
            {
                throw new Exception($"{(waitForMsg != null ? $"WaitFor: '{waitForMsg}'" : "Wait")} failed." + $"{Environment.NewLine}Error: {e.Message}");
            }
        }

        public static T WaitUntil<T>(
            T target,
            Func<bool> condition,
            Action<T> continueWaitOnlyWhen,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            return WaitUntil(target, condition, continueWaitOnlyWhen?.ToFunc(), maxWaitSeconds, waitForMsg);
        }

        /// <summary>
        /// Waits until a valid result is obtained from a specified function.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TAny">The type of the object returned by the continueWaitOnlyWhen function.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="getResult">A function that takes the target object and returns a result.</param>
        /// <param name="condition">A function that takes the result and returns a boolean indicating whether the result is valid.</param>
        /// <param name="continueWaitOnlyWhen">A function that takes the target object and returns an object of type TAny. The wait continues only when this function is invoked.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for a valid result. The default is 60 seconds.</param>
        /// <param name="waitForMsg">A message that is included in the exception if the wait times out.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The valid result.</returns>
        /// <exception cref="TimeoutException">Thrown if the wait times out before a valid result is obtained.</exception>
        /// <exception cref="Exception">Thrown if an error occurs while waiting for a valid result.</exception>
        public static async Task<TResult> WaitUntilGetValidResultAsync<T, TResult, TAny>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            Func<T, TAny> continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null,
            CancellationToken cancellationToken = default)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            try
            {
                var result = getResult(target);

                while (!condition(result))
                {
                    if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();

                    // Retry check condition again to continueWaitOnlyWhen throw error only when condition not matched
                    // Sometime when continueWaitOnlyWhen execute the condition is matched
                    await WaitRetryThrowFinalExceptionAsync(
                        () =>
                        {
                            result = getResult(target);

                            if (!condition(result)) continueWaitOnlyWhen?.Invoke(target);

                            return Task.CompletedTask;
                        },
                        cancellationToken: cancellationToken);

                    if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    {
                        await Task.Delay((int)(DefaultWaitIntervalSeconds * 1000), cancellationToken);
                        result = getResult(target);
                    }
                    else
                    {
                        throw new TimeoutException(
                            $"WaitUntilGetValidResult is timed out (Max: {maxWaitSeconds} seconds)." +
                            $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                throw new Exception($"{(waitForMsg != null ? $"WaitFor: '{waitForMsg}'" : "Wait")} failed." + $"{Environment.NewLine}Error: {e.Message}");
            }
        }

        /// <summary>
        /// Waits until a valid result is returned based on a provided condition.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="getResult">A function that gets the result from the target.</param>
        /// <param name="condition">A function that determines the validity of the result.</param>
        /// <param name="continueWaitOnlyWhen">An action that specifies when to continue waiting. If null, the wait will continue until the condition is met or the maximum wait time is exceeded.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait in seconds. Defaults to DefaultWaitUntilMaxSeconds.</param>
        /// <param name="waitForMsg">An optional message that can be displayed while waiting.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The valid result.</returns>
        public static Task<TResult> WaitUntilGetValidResultAsync<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            Action<T> continueWaitOnlyWhen,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null,
            CancellationToken cancellationToken = default)
        {
            return WaitUntilGetValidResultAsync(target, getResult, condition, continueWaitOnlyWhen?.ToFunc(), maxWaitSeconds, waitForMsg, cancellationToken);
        }

        public static void WaitUntilSuccess<T>(
            T target,
            Action<T> action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            WaitUntilSuccess<T, object>(target, action, null, maxWaitSeconds, waitForMsg);
        }

        /// <summary>
        /// Executes the specified action on the target until it succeeds or the maximum wait time is exceeded.
        /// </summary>
        /// <typeparam name="T">The type of the target on which the action is performed.</typeparam>
        /// <typeparam name="TAny">TAny</typeparam>
        /// <param name="target">The target on which the action is performed.</param>
        /// <param name="action">The action to be performed on the target.</param>
        /// <param name="continueWaitOnlyWhen">continueWaitOnlyWhen</param>
        /// <param name="maxWaitSeconds">The maximum time to wait for the action to succeed, in seconds. Default is 60 seconds.</param>
        /// <param name="waitForMsg">Optional message to be displayed while waiting for the action to succeed.</param>
        /// <exception cref="TimeoutException">Thrown when the maximum wait time is exceeded before the action succeeds.</exception>
        /// <exception cref="Exception">Thrown when an error occurs while performing the action.</exception>
        public static void WaitUntilSuccess<T, TAny>(
            T target,
            Action<T> action,
            Func<T, TAny> continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            try
            {
                while (true)
                {
                    continueWaitOnlyWhen?.Invoke(target);

                    try
                    {
                        action(target);
                    }
                    catch (Exception e)
                    {
                        if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                            Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));
                        else
                            throw new TimeoutException(
                                $"WaitUntilGetSuccess is timed out (Max: {maxWaitSeconds} seconds)." +
                                $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}" +
                                $"{Environment.NewLine}Error: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"{(waitForMsg != null ? $"WaitFor: '{waitForMsg}'" : "Wait")} failed." + $"{Environment.NewLine}Error: {e.Message}");
            }
        }


        public static Task<TResult> WaitUntilGetSuccessAsync<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            return WaitUntilGetSuccessAsync(target, getResult, null, maxWaitSeconds, waitForMsg);
        }

        /// <summary>
        /// Waits until the specified function returns a successful result.
        /// </summary>
        /// <typeparam name="T">The type of the target object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TAny">The type of the object returned by the continueWaitOnlyWhen function.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="getResult">A function that takes the target object and returns a result.</param>
        /// <param name="continueWaitOnlyWhen">A function that takes the target object and returns an object. If this function is provided, the method will continue waiting only when this function returns a non-null value.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for a successful result. The default value is 60 seconds.</param>
        /// <param name="waitForMsg">An optional message to display while waiting for a successful result.</param>
        /// <returns>The result returned by the getResult function.</returns>
        /// <exception cref="Exception">Thrown when the result is null.</exception>
        /// <exception cref="TimeoutException">Thrown when the maximum wait time is exceeded.</exception>
        public static async Task<TResult> WaitUntilGetSuccessAsync<T, TResult, TAny>(
            T target,
            Func<T, TResult> getResult,
            Func<T, TAny> continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            try
            {
                while (true)
                {
                    continueWaitOnlyWhen?.Invoke(target);

                    try
                    {
                        var result = getResult(target);

                        return result == null ? throw new Exception("Result must be not null") : result;
                    }
                    catch (Exception e)
                    {
                        if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                            await Task.Delay((int)(DefaultWaitIntervalSeconds * 1000));
                        else
                            throw new TimeoutException(
                                $"WaitUntilGetSuccess is timed out (Max: {maxWaitSeconds} seconds)." +
                                $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}" +
                                $"{Environment.NewLine}Error: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"{(waitForMsg != null ? $"WaitFor: '{waitForMsg}'" : "Wait")} failed." + $"{Environment.NewLine}Error: {e.Message}");
            }
        }

        public static Task<TResult> WaitUntilGetSuccessAsync<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Action<T> continueWaitOnlyWhen,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            return WaitUntilGetSuccessAsync(target, getResult, continueWaitOnlyWhen?.ToFunc(), maxWaitSeconds, waitForMsg);
        }

        /// <summary>
        /// Waits until a specified condition is met, then performs a specified action.
        /// </summary>
        /// <typeparam name="T">The type of the result produced by the action.</typeparam>
        /// <param name="condition">A function that returns a boolean value indicating whether the condition is met.</param>
        /// <param name="action">A function that returns a value of type T. This function is executed when the condition is met.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for the condition to be met. The default value is DefaultWaitUntilMaxSeconds.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds between checks of the condition. The default value is DefaultWaitIntervalSeconds.</param>
        /// <param name="waitForMsg">An optional message that can be included in the TimeoutException if the condition is not met within the specified time.</param>
        /// <returns>The result of the action function if the condition is met within the specified time.</returns>
        /// <exception cref="TimeoutException">Thrown if the condition is not met within the specified time.</exception>
        public static T WaitUntilToDo<T>(
            Func<bool> condition,
            Func<T> action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return action();
        }

        /// <summary>
        /// Asynchronously waits until a specified condition is met, then performs a specified action.
        /// </summary>
        /// <param name="condition">A function that returns a Task[bool] representing the condition to be met.</param>
        /// <param name="action">A function that returns a Task representing the action to be performed once the condition is met.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for the condition to be met. Defaults to DefaultWaitUntilMaxSeconds.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds between each check of the condition. Defaults to DefaultWaitIntervalSeconds.</param>
        /// <param name="waitForMsg">An optional message to be included in the TimeoutException if the condition is not met within the specified maxWaitSeconds.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="TimeoutException">Thrown if the condition is not met within the specified maxWaitSeconds.</exception>
        public static async Task WaitUntilToDo(
            Func<Task<bool>> condition,
            Func<Task> action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!await condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    await Task.Delay((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            await action();
        }

        /// <summary>
        /// Waits until a specified condition is met, then executes a specified action.
        /// </summary>
        /// <param name="condition">A function that returns a boolean value indicating whether a certain condition is met.</param>
        /// <param name="action">The action to be executed once the condition is met.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for the condition to be met. The default is <see cref="DefaultWaitUntilMaxSeconds" />.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds to wait between checks of the condition. The default is <see cref="DefaultWaitIntervalSeconds" />.</param>
        /// <param name="waitForMsg">An optional message to be displayed while waiting for the condition to be met.</param>
        /// <exception cref="TimeoutException">Thrown when the condition is not met within the specified maximum wait time.</exception>
        /// <remarks>
        /// This method uses a busy wait, repeatedly checking the condition and sleeping for a specified interval between checks. If the condition is not met within the specified maximum wait time, a TimeoutException is thrown.
        /// </remarks>
        public static void WaitUntilToDo(
            Func<bool> condition,
            Action action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            action();
        }

        /// <summary>
        /// Waits until the specified condition is met, or until the maximum wait time has elapsed.
        /// </summary>
        /// <typeparam name="TSource">The type of the source object.</typeparam>
        /// <typeparam name="TTarget">The type of the target object.</typeparam>
        /// <param name="whenDo">The condition to be met.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait. The default is <see cref="DefaultWaitUntilMaxSeconds" />.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds between each check of the condition. The default is <see cref="DefaultWaitIntervalSeconds" />.</param>
        /// <param name="waitForMsg">An optional message to be displayed while waiting.</param>
        /// <returns>The result of the execution of the condition once it is met.</returns>
        /// <exception cref="TimeoutException">Thrown if the maximum wait time is exceeded before the condition is met.</exception>
        public static TTarget WaitUntilHasMatchedCase<TSource, TTarget>(
            WhenCase<TSource, TTarget> whenDo,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            while (!whenDo.HasMatchedCase())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilHasMatchedCase is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return whenDo.Execute();
        }

        public static TSource WaitUntilHasMatchedCase<TSource>(
            WhenCase<TSource> whenDo,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            WaitUntilHasMatchedCase(whenDo.As<WhenCase<TSource, ValueTuple>>(), maxWaitSeconds, waitIntervalSeconds, waitForMsg);

            return whenDo.Source;
        }

        /// <summary>
        /// Executes the specified action and then waits for a condition to be met before repeating the action.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="until">The condition that must be met to stop the execution of the action.</param>
        /// <param name="maxWaitSeconds">The maximum time in seconds to wait before throwing a TimeoutException. Default is 60 seconds.</param>
        /// <param name="waitIntervalSeconds">The time in seconds to wait between each execution of the action. Default is 0.3 seconds.</param>
        /// <param name="waitForMsg">The message to be included in the TimeoutException if the 'until' condition is not met within 'maxWaitSeconds'.</param>
        /// <exception cref="TimeoutException">Thrown when the 'until' condition is not met within 'maxWaitSeconds'.</exception>
        /// <remarks>
        /// This method is useful for retrying an operation until a certain condition is met, such as waiting for a web page to load or a file to become available.
        /// </remarks>
        public static void WaitRetryDoUntil(
            Action action,
            Func<bool> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            action();

            while (!until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitRetryDoUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

                action();
            }
        }

        /// <summary>
        /// Executes a specified action repeatedly until a certain condition is met or a timeout occurs.
        /// </summary>
        /// <typeparam name="T">The type of the result produced by the action.</typeparam>
        /// <param name="action">The action to be executed.</param>
        /// <param name="until">The condition that determines when to stop executing the action.</param>
        /// <param name="maxWaitSeconds">The maximum amount of time (in seconds) to wait for the condition to be met before a timeout occurs. Default is <see cref="DefaultWaitUntilMaxSeconds" />.</param>
        /// <param name="waitIntervalSeconds">The interval (in seconds) between each execution of the action. Default is <see cref="DefaultWaitIntervalSeconds" />.</param>
        /// <param name="waitForMsg">An optional message that describes what the method is waiting for. This message is included in the TimeoutException if a timeout occurs.</param>
        /// <returns>The result of the last execution of the action.</returns>
        /// <exception cref="TimeoutException">Thrown when the specified maximum wait time is exceeded.</exception>
        public static T WaitRetryDoUntil<T>(
            Func<T> action,
            Func<bool> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            var result = action();

            while (!until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitRetryDoUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

                result = action();
            }

            return result;
        }

        /// <summary>
        /// Executes a specified action repeatedly until a specified condition is met or a timeout occurs.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="until">The condition that stops the execution of the action.</param>
        /// <param name="maxWaitSeconds">The maximum amount of time in seconds to wait for the condition to be met. Default is 60 seconds.</param>
        /// <param name="waitIntervalSeconds">The interval in seconds between each execution of the action. Default is 0.3 seconds.</param>
        /// <param name="waitForMsg">Optional message to be included in the TimeoutException if the condition is not met within the specified time.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="TimeoutException">Thrown when the condition is not met within the specified time.</exception>
        /// <remarks>
        /// This method is useful for scenarios where an operation needs to be retried until a certain condition is met, such as waiting for a resource to become available.
        /// </remarks>
        public static async Task WaitRetryDoUntilAsync(
            Func<Task> action,
            Func<Task<bool>> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            await action();
            await Task.Delay((int)(waitIntervalSeconds * 1000));

            while (!await until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds >= maxWaitMilliseconds)
                    throw new TimeoutException(
                        $"DoUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

                await action();
                await Task.Delay((int)(waitIntervalSeconds * 1000));
            }
        }

        /// <summary>
        /// Executes a specified asynchronous action repeatedly until a certain condition is met or a timeout occurs.
        /// </summary>
        /// <typeparam name="T">The type of the result returned by the action.</typeparam>
        /// <param name="action">The asynchronous action to be executed.</param>
        /// <param name="until">The asynchronous function that determines the condition for the action to stop repeating.</param>
        /// <param name="maxWaitSeconds">The maximum time in seconds to wait for the 'until' condition to be met before a timeout occurs. Default is 'DefaultWaitUntilMaxSeconds'.</param>
        /// <param name="waitIntervalSeconds">The time interval in seconds between each execution of the action. Default is 'DefaultWaitIntervalSeconds'.</param>
        /// <param name="waitForMsg">Optional message to be included in the TimeoutException if it occurs.</param>
        /// <returns>The result of the last execution of the action.</returns>
        /// <exception cref="TimeoutException">Thrown when the 'until' condition is not met within the specified 'maxWaitSeconds'.</exception>
        public static async Task<T> WaitRetryDoUntilAsync<T>(
            Func<Task<T>> action,
            Func<Task<bool>> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            var result = await action();
            await Task.Delay((int)(waitIntervalSeconds * 1000));

            while (!await until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds >= maxWaitMilliseconds)
                    throw new TimeoutException(
                        $"DoUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

                result = await action();
                await Task.Delay((int)(waitIntervalSeconds * 1000));
            }

            return result;
        }

        /// <summary>
        /// Execute an action with timeout. Return false is it's timed out. Return true if the action execute successfully
        /// </summary>
        public static async Task<bool> RunWithTimeout(
            Func<CancellationToken, Task> fn,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            Action? onTimeoutAction = null)
        {
            var (isInTime, _) = await RunWithTimeout(
                ct => fn(ct).Then(ValueTuple.Create),
                timeout,
                cancellationToken,
                onTimeoutAction);

            return isInTime;
        }

        /// <summary>
        /// Execute an action with timeout. Return (isInTime (value is false), result (default type value)) with default action result type is it's timed out. Return (isInTime (value is true), result) true with action result if the action execute successfully
        /// </summary>
        public static async Task<ValueTuple<bool, TResult>> RunWithTimeout<TResult>(
            Func<CancellationToken, Task<TResult>> fn,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            Action? onTimeoutAction = null)
        {
            if (timeout.TotalMilliseconds <= 0) return (true, await fn(cancellationToken));

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                var mainFnTask = fn(cts.Token);
                var timeoutTask = Task.Delay(timeout, cts.Token).Then(() => false).ThenAction(_ => cts?.Cancel());

                await Task.WhenAny(mainFnTask, timeoutTask);

                var isInTime = !timeoutTask.IsCompletedSuccessfully;
                var result = mainFnTask.IsCompletedSuccessfully ? mainFnTask.Result : default;

                if (!isInTime) onTimeoutAction?.Invoke();

                return (isInTime, result);
            }
            catch (TaskCanceledException)
            {
                return (false, default);
            }
            finally
            {
                cts.Dispose();
                cts = null;
            }
        }

        /// <inheritdoc cref="RunWithTimeout(Func{CancellationToken,Task},TimeSpan,CancellationToken,Action?)" />
        public static Task<(bool, TResult)> RunWithTimeout<TResult>(
            Func<TResult> fn,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            Action? onTimeoutAction = null)
        {
            return RunWithTimeout(cts => Task.Run(fn, cts), timeout, cancellationToken, onTimeoutAction);
        }

        public static Task<bool> RunWithTimeout(Func<Task> fn, TimeSpan timeout, CancellationToken cancellationToken = default, Action? onTimeoutAction = null)
        {
            return RunWithTimeout(cts => fn(), timeout, cancellationToken, onTimeoutAction);
        }

        /// <summary>
        /// Executes a provided asynchronous function in parallel for each item in the given enumerable.
        /// </summary>
        /// <typeparam name="T">The type of the items in the enumerable.</typeparam>
        /// <typeparam name="TResult">The type of the result returned by the provided function.</typeparam>
        /// <param name="items">The enumerable of items to process.</param>
        /// <param name="action">The asynchronous function to execute for each item. This function takes an item and its index as parameters and returns a task that represents the operation.</param>
        /// <param name="maxConcurrent">The maximum number of concurrent operations. Default value is <see cref="DefaultParallelIoTaskMaxConcurrent" />.</param>
        /// <returns>A task that represents the operation. The result of the task is a list of results returned by the provided function.</returns>
        public static async Task<List<TResult>> ParallelAsync<TResult, T>(IEnumerable<T> items, Func<T, int, Task<TResult>> action, int? maxConcurrent)
        {
            var maxDegreeOfParallelism = maxConcurrent ?? DefaultParallelIoTaskMaxConcurrent;
            var itemsList = items.As<IList<T>>() ?? items.ToList();

            try
            {
                if (itemsList.Count == 0)
                    return [];
                if (itemsList.Count == 1)
                    return [await action(itemsList.First(), 0)];
                if (itemsList.Count <= maxDegreeOfParallelism)
                    return await Task.WhenAll(itemsList.Select((p, i) => action(p, i))).Then(results => results.ToList());

                // Handle parallelism with limit maxDegreeOfParallelism
                var actionQueue = new Queue<(int, Func<Task<(int, TResult, Exception?)>>)>(
                    itemsList
                        .Select<T, ValueTuple<int, Func<Task<ValueTuple<int, TResult, Exception?>>>>>(
                            (item, i) => (i,
                                () => action(item, i)
                                    .Then<TResult, ValueTuple<TResult, Exception?>>(p => (p, null))
                                    .Recover(ex => (default!, ex))
                                    .Then<ValueTuple<TResult, Exception?>, ValueTuple<int, TResult, Exception?>>(
                                        itemActionResult => (i, itemActionResult.Item1, itemActionResult.Item2)))));
                var processingActionTasks = new Dictionary<int, Task<ValueTuple<int, TResult, Exception?>>>();
                var processedActionTaskResults = new Dictionary<int, ValueTuple<TResult, Exception?>>();
                var processedFailedActionExceptions = new List<Exception>();

                while (processedActionTaskResults.Count < itemsList.Count)
                {
                    while (processingActionTasks.Count < maxDegreeOfParallelism && actionQueue.Count > 0)
                    {
                        var numberOfToAddItemsToMaxParallelism = maxDegreeOfParallelism - processingActionTasks.Count;

                        for (var i = 0; i < numberOfToAddItemsToMaxParallelism; i++)
                        {
                            if (actionQueue.TryDequeue(out var queueItem))
                            {
                                var (nextProcessItemIndex, nextProcessAction) = queueItem;

                                processingActionTasks.TryAdd(nextProcessItemIndex, nextProcessAction());
                            }
                        }
                    }

                    await Task.WhenAny(processingActionTasks.Values);

                    processingActionTasks
                        .Where(p => p.Value.IsCompleted)
                        .ForEach(
                            completedTaskItem =>
                            {
                                var (nextProcessedItemIndex, nextProcessedActionResult, nextProcessedActionException) = completedTaskItem.Value.Result;

                                processedActionTaskResults.TryAdd(nextProcessedItemIndex, (nextProcessedActionResult, nextProcessedActionException));
                                processingActionTasks.Remove(nextProcessedItemIndex, out _);
                                if (nextProcessedActionException != null) processedFailedActionExceptions.Add(nextProcessedActionException);
                            });
                }

                return processedFailedActionExceptions.Count == 0
                    ? processedActionTaskResults.OrderBy(p => p.Key).Select(p => p.Value.Item1).ToList()
                    : throw new AggregateException(processedFailedActionExceptions);
            }
            finally
            {
                if (itemsList.Count >= maxDegreeOfParallelism) await GarbageCollector.Collect();
            }
        }

        /// <summary>
        /// Provides a mechanism to throttle the execution of tasks.
        /// </summary>
        /// <remarks>
        /// The Throttler class is used to control the rate of execution of tasks.
        /// It provides a way to ensure that a task does not execute more frequently than a specified interval.
        /// </remarks>
        public class Throttler : IDisposable
        {
            private readonly CancellationTokenSource cts = new();
            private readonly SemaphoreSlim lockObj = new(1, 1);
            private bool disposed;
            private volatile bool isExecuting;
            private DateTime? lastExecutionTime;

            public Throttler(TimeSpan throttleWindow)
            {
                ThrottleWindow = throttleWindow;
            }

            public TimeSpan ThrottleWindow { get; init; }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Executes the provided function in a throttled manner.
            /// </summary>
            public async Task ThrottleExecute(Func<Task> action)
            {
                if (isExecuting || (lastExecutionTime != null && lastExecutionTime.Value.Add(ThrottleWindow) < DateTime.UtcNow) || lockObj.CurrentCount == 0) return;

                try
                {
                    isExecuting = true;

                    await lockObj.WaitAsync();

                    await action();

                    lastExecutionTime = DateTime.UtcNow;
                }
                finally
                {
                    isExecuting = false;

                    lockObj.Release();
                }
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        // Release managed resources
                        cts?.Dispose();
                        lockObj.Dispose();
                    }

                    // Release unmanaged resources

                    disposed = true;
                }
            }

            ~Throttler()
            {
                Dispose(false);
            }
        }

        /// <summary>
        /// A queue that processes tasks in parallel with a specified maximum number of concurrent tasks.
        /// Tasks are automatically dequeued and processed. The number of concurrent tasks is controlled by a SemaphoreSlim.
        /// </summary>
        /// <typeparam name="TQueueItem">The type of item being processed in the queue.</typeparam>
        public class ParallelTasksQueue<TQueueItem> : IDisposable
        {
            protected readonly SemaphoreSlim MaxParallelLock; // Semaphore to throttle concurrent tasks
            private readonly Func<TQueueItem, CancellationToken, Task> processQueueItemFn;
            private volatile bool checkToDequeueAndProcessItemRunning; // Atomic flag to prevent concurrent dequeue attempts
            private bool disposed; // Flag to indicate if the object has been disposed

            /// <summary>
            /// Initializes a new instance of the <see cref="ParallelTasksQueue{TQueueItem}" /> class.
            /// </summary>
            /// <param name="processQueueItemFn">A function that processes each queue item asynchronously.</param>
            /// <param name="maxParallelProcessCount">The maximum number of concurrent tasks allowed.</param>
            /// <param name="cancellationToken">A cancellation token used to stop queue processing.</param>
            public ParallelTasksQueue(
                Func<TQueueItem, CancellationToken, Task> processQueueItemFn,
                int maxParallelProcessCount,
                CancellationToken cancellationToken)
            {
                this.processQueueItemFn = processQueueItemFn;
                CancellationToken = cancellationToken;
                MaxParallelLock = new SemaphoreSlim(maxParallelProcessCount, maxParallelProcessCount); // Initialize the semaphore with the maximum count
            }

            /// <summary>
            /// Gets the queue that holds the items to be processed.
            /// </summary>
            public ConcurrentQueue<TQueueItem> TaskItemQueue { get; } = new();

            /// <summary>
            /// Gets or sets the CancellationToken used to stop processing.
            /// </summary>
            public CancellationToken CancellationToken { get; set; }

            /// <summary>
            /// Disposes the resources used by the queue, ensuring all tasks are canceled and semaphore is released.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this); // Suppress finalization to avoid unnecessary cleanup
            }

            /// <summary>
            /// Enqueues a new item to the queue and triggers the dequeue process if not already running.
            /// </summary>
            /// <param name="taskItem">The item to be processed.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            public async Task EnqueueAsync(TQueueItem taskItem)
            {
                TaskItemQueue.Enqueue(taskItem); // Add the task item to the queue
                _ = CheckToDequeueAndProcessItemAsync(); // Ensure the queue is being processed. Process the queue item in a separate task to allow EnqueueAsync finished
            }

            /// <summary>
            /// Asynchronously checks the queue and processes items while respecting the maximum number of concurrent tasks.
            /// </summary>
            /// <returns>A task representing the asynchronous operation.</returns>
            private async Task CheckToDequeueAndProcessItemAsync()
            {
                if (checkToDequeueAndProcessItemRunning) return; // Avoid multiple concurrent calls to this method

                checkToDequeueAndProcessItemRunning = true; // Set flag to indicate that dequeuing is in progress

                try
                {
                    while (!CancellationToken.IsCancellationRequested &&
                           !disposed &&
                           MaxParallelLock.CurrentCount > 0 &&
                           TaskItemQueue.TryDequeue(out var taskItem))
                    {
                        // Wait for an available slot in the semaphore to ensure we don't exceed the parallel task limit
                        await MaxParallelLock.WaitAsync(CancellationToken);

                        // Process the queue item in a separate task to allow further dequeueing
                        _ = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    await processQueueItemFn(taskItem, CancellationToken); // Process the task item
                                }
                                finally
                                {
                                    MaxParallelLock.Release(); // Release the semaphore slot after processing
                                    await CheckToDequeueAndProcessItemAsync(); // Continue processing the next item in the queue
                                }
                            },
                            CancellationToken);
                    }
                }
                finally
                {
                    checkToDequeueAndProcessItemRunning = false; // Reset flag once all items are processed
                }
            }

            /// <summary>
            /// Releases the unmanaged resources used by the class and optionally disposes of the managed resources.
            /// </summary>
            /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
            protected virtual void Dispose(bool disposing)
            {
                if (disposing && !disposed)
                {
                    MaxParallelLock.Dispose();
                    disposed = true; // Mark as disposed
                }
            }
        }
    }
}
