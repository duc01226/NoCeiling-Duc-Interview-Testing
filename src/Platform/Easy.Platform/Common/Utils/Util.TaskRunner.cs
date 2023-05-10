using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Extensions.WhenCases;
using Microsoft.Extensions.Logging;
using Polly;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class TaskRunner
    {
        public const int DefaultWaitUntilMaxSeconds = 60;
        public const double DefaultWaitIntervalSeconds = 0.3;

        public const int DefaultWhenAllWaitEachItemsToKeepStackTraceMaxTasksCount = 5;

        /// <summary>
        /// Execute an action after a given of time.
        /// </summary>
        public static async Task QueueDelayAsyncAction(
            Func<CancellationToken, Task> action,
            TimeSpan delayTime,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayTime, cancellationToken);
            await action(cancellationToken);
        }

        /// <summary>
        /// Execute an action after a given of time.
        /// </summary>
        public static async Task QueueDelayAction(
            Action action,
            TimeSpan delayTime,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayTime, cancellationToken);
            action();
        }

        public static void QueueActionInBackground(
            Func<CancellationToken, Task> action,
            ILogger logger,
            int delayTimeSeconds = 0,
            CancellationToken cancellationToken = default)
        {
            Task.Run(
                async () =>
                {
                    try
                    {
                        await QueueDelayAsyncAction(async token => await action(token), TimeSpan.FromSeconds(delayTimeSeconds), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, ex.Message);
                        throw;
                    }
                },
                cancellationToken);
        }

        public static void QueueActionInBackground(
            Func<Task> action,
            ILogger logger,
            int delayTimeSeconds = 0,
            CancellationToken cancellationToken = default)
        {
            QueueActionInBackground(async _ => await action(), logger, delayTimeSeconds, cancellationToken);
        }

        public static void QueueActionInBackground(
            Action action,
            ILogger logger,
            int delayTimeSeconds = 0,
            CancellationToken cancellationToken = default)
        {
            Task.Run(
                async () =>
                {
                    try
                    {
                        await QueueDelayAction(action, TimeSpan.FromSeconds(delayTimeSeconds), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, ex.Message);
                        throw;
                    }
                },
                cancellationToken);
        }


        public static async Task QueueIntervalAsyncAction(
            Func<CancellationToken, Task> action,
            int intervalTimeInSeconds,
            int? maximumIntervalExecutionCount = null,
            bool executeOnceImmediately = false,
            CancellationToken cancellationToken = default)
        {
            if (executeOnceImmediately) await action(cancellationToken);

            if (maximumIntervalExecutionCount <= 0) return;

            var executionCount = 0;
            while (executionCount < maximumIntervalExecutionCount)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalTimeInSeconds), cancellationToken);
                await action(cancellationToken);
                executionCount += 1;
            }
        }

        public static void QueueIntervalAsyncActionInBackground(
            Func<CancellationToken, Task> action,
            int intervalTimeInSeconds,
            ILogger logger,
            int? maximumIntervalExecutionCount = null,
            bool executeOnceImmediately = false,
            CancellationToken cancellationToken = default)
        {
            Task.Run(
                async () =>
                {
                    try
                    {
                        await QueueIntervalAsyncAction(action, intervalTimeInSeconds, maximumIntervalExecutionCount, executeOnceImmediately, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, ex.Message);
                        throw;
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

        public static async Task CatchException(Func<Task> action, Action<Exception> onException = null)
        {
            await CatchException<Exception>(action, onException);
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

        public static async Task<T> CatchExceptionContinueThrowAsync<T>(Func<Task<T>> func, Action<Exception> onException)
        {
            return await CatchExceptionContinueThrowAsync<T, Exception>(async () => await func(), onException);
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

        public static async Task WhenAll(params Task[] tasks)
        {
            // If not too much task using WaitResult for keeping the track trace
            // Too much task using this locking could lead to Deadlocks or Threadpool Starvation
            // https://www.nikouusitalo.com/blog/why-is-getawaiter-getresult-bad-in-c/
            if (tasks.Length <= DefaultWhenAllWaitEachItemsToKeepStackTraceMaxTasksCount)
                await tasks.ForEachAsync(async p => await p);
            else await Task.WhenAll(tasks);
        }

        public static async Task WhenAll(IEnumerable<Task> tasks)
        {
            var taskList = tasks.ToList();

            // If not too much task using WaitResult for keeping the track trace
            // Too much task using this locking could lead to Deadlocks or Threadpool Starvation
            // https://www.nikouusitalo.com/blog/why-is-getawaiter-getresult-bad-in-c/
            if (taskList.Count <= DefaultWhenAllWaitEachItemsToKeepStackTraceMaxTasksCount)
                await taskList.ForEachAsync(async p => await p);
            else await Task.WhenAll(taskList);
        }

        public static async Task<List<T>> WhenAll<T>(IEnumerable<Task<T>> tasks)
        {
            var taskList = tasks.ToList();

            // If not too much task using WaitResult for keeping the track trace
            // Too much task using this locking could lead to Deadlocks or Threadpool Starvation
            // https://www.nikouusitalo.com/blog/why-is-getawaiter-getresult-bad-in-c/
            if (taskList.Count <= DefaultWhenAllWaitEachItemsToKeepStackTraceMaxTasksCount)
                return await taskList.SelectAsync(async p => await p);
            return await Task.WhenAll(taskList).Then(_ => _.ToList());
        }

        public static async Task<List<T>> WhenAll<T>(params Task<T>[] tasks)
        {
            // If not too much task using WaitResult for keeping the track trace
            // Too much task using this locking could lead to Deadlocks or Threadpool Starvation
            // https://www.nikouusitalo.com/blog/why-is-getawaiter-getresult-bad-in-c/
            if (tasks.Length <= DefaultWhenAllWaitEachItemsToKeepStackTraceMaxTasksCount)
                return await tasks.SelectAsync(async p => await p);
            return await Task.WhenAll(tasks).Then(_ => _.ToList());
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

        public static async Task<T> Async<T>(T t)
        {
            return await Task.FromResult(t);
        }

        /// <summary>
        /// WaitRetryThrowFinalExceptionAsync. Throw final exception on max retry reach
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="retryCount"></param>
        /// <param name="sleepDurationProvider">Ex: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))</param>
        /// <param name="executeFunc"></param>
        /// <param name="onBeforeThrowFinalExceptionFn"></param>
        /// <param name="onRetry">onRetry: (exception,timeSpan,currentRetry,context)</param>
        /// <returns></returns>
        public static async Task WaitRetryThrowFinalExceptionAsync<TException>(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            await Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalExceptionAsync(
                    async () => await executeFunc(),
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context})" />
        public static async Task<T> WaitRetryThrowFinalExceptionAsync<T, TException>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            return await Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => DefaultWaitIntervalSeconds.Seconds()),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalExceptionAsync(
                    async () => await executeFunc(),
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context})" />
        public static async Task WaitRetryThrowFinalExceptionAsync(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            await WaitRetryThrowFinalExceptionAsync<Exception>(
                async () => await executeFunc(),
                sleepDurationProvider,
                retryCount,
                onBeforeThrowFinalExceptionFn,
                onRetry);
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context})" />
        public static async Task<T> WaitRetryThrowFinalExceptionAsync<T>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return await WaitRetryThrowFinalExceptionAsync<T, Exception>(
                async () => await executeFunc(),
                sleepDurationProvider,
                retryCount,
                onBeforeThrowFinalExceptionFn,
                onRetry);
        }

        public static async Task<PolicyResult> WaitRetryAsync(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCaptureAsync(
                    async () => await executeFunc());
        }

        public static async Task<PolicyResult<T>> WaitRetryAsync<T>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCaptureAsync(
                    async () => await executeFunc());
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
            int retryCount = 1,
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

        public static PolicyResult<T> WaitRetry<T>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(executeFunc);
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
            int retryCount = 1,
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
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(
                    executeAction);
        }

        /// <inheritdoc cref="WaitRetryThrowFinalException{T}" />
        public static T WaitRetryThrowFinalException<T, TException>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
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

        public static PolicyResult<T> WaitRetry<T, TException>(
            Func<T> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetry(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultWaitIntervalSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(
                    executeFunc);
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

        public static void WaitUntil(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            WaitUntilToDo(condition, () => { }, maxWaitSeconds, waitIntervalSeconds, waitForMsg);
        }

        public static void TryWaitUntil(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            CatchException(() => WaitUntil(condition, maxWaitSeconds, waitIntervalSeconds, waitForMsg));
        }

        public static TResult WaitUntilGetValidResult<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));

            while (!condition(getResult(target)))
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilGetValidResult is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return getResult(target);
        }

        /// <summary>
        /// Wait until the condition met. Stop wait immediately if continueWaitOnlyWhen assert failed throw exception
        /// </summary>
        public static T WaitUntil<T, TAny>(
            T target,
            Func<bool> condition,
            Func<T, TAny>? continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));

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
            return WaitUntil(target, condition, continueWaitOnlyWhen.ToFunc(), maxWaitSeconds, waitForMsg);
        }

        public static TResult WaitUntilGetValidResult<T, TResult, TAny>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            Func<T, TAny>? continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));

            try
            {
                while (!condition(getResult(target)))
                {
                    // Retry check condition again to continueWaitOnlyWhen throw error only when condition not matched
                    // Sometime when continueWaitOnlyWhen execute the condition is matched
                    WaitRetryThrowFinalException(
                        () =>
                        {
                            if (!condition(getResult(target))) continueWaitOnlyWhen?.Invoke(target);
                        });

                    if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                        Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));
                    else
                        throw new TimeoutException(
                            $"WaitUntilGetValidResult is timed out (Max: {maxWaitSeconds} seconds)." +
                            $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
                }

                return getResult(target);
            }
            catch (Exception e)
            {
                throw new Exception($"{(waitForMsg != null ? $"WaitFor: '{waitForMsg}'" : "Wait")} failed." + $"{Environment.NewLine}Error: {e.Message}");
            }
        }

        public static TResult WaitUntilGetValidResult<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            Action<T> continueWaitOnlyWhen,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            return WaitUntilGetValidResult(target, getResult, condition, continueWaitOnlyWhen.ToFunc(), maxWaitSeconds, waitForMsg);
        }

        public static TResult WaitUntilGetSuccess<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            return WaitUntilGetSuccess(target, getResult, continueWaitOnlyWhen: null, maxWaitSeconds, waitForMsg);
        }

        public static TResult WaitUntilGetSuccess<T, TResult, TAny>(
            T target,
            Func<T, TResult?> getResult,
            Func<T, TAny>? continueWaitOnlyWhen = null,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultWaitIntervalSeconds * 1000));

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

        public static TResult WaitUntilGetSuccess<T, TResult>(
            T target,
            Func<T, TResult> getResult,
            Action<T> continueWaitOnlyWhen,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            return WaitUntilGetSuccess(target, getResult, continueWaitOnlyWhen.ToFunc(), maxWaitSeconds, waitForMsg);
        }

        public static T WaitUntilToDo<T>(
            Func<bool> condition,
            Func<T> action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(waitIntervalSeconds * 1000));

            while (!condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return action();
        }

        public static void WaitUntilToDo(
            Func<bool> condition,
            Action action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            WaitUntilToDo(condition, action.ToFunc(), maxWaitSeconds, waitIntervalSeconds, waitForMsg);
        }

        public static TTarget WaitUntilHasMatchedCase<TSource, TTarget>(
            WhenCase<TSource, TTarget> whenDo,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(waitIntervalSeconds * 1000));
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

        public static void WaitRetryDoUntil(
            Action action,
            Func<bool> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            WaitRetryDoUntil(action.ToFunc(), until, maxWaitSeconds, waitIntervalSeconds, waitForMsg);
        }

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

        public static async Task WaitRetryDoUntilAsync(
            Func<Task> action,
            Func<Task<bool>> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double waitIntervalSeconds = DefaultWaitIntervalSeconds,
            string waitForMsg = null)
        {
            await WaitRetryDoUntilAsync(action.ToAsyncFunc(), until, maxWaitSeconds, waitIntervalSeconds, waitForMsg);
        }

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

            while (!await until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(waitIntervalSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"DoUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

                result = await action();
            }

            return result;
        }
    }
}
