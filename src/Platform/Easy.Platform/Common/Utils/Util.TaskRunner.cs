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
        public const double DefaultMinimumDelayWaitSeconds = 0.3;

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
                        await QueueDelayAsyncAction(action, TimeSpan.FromSeconds(delayTimeSeconds), cancellationToken);
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
            QueueActionInBackground(_ => action(), logger, delayTimeSeconds, cancellationToken);
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

        public static async Task<T> CatchExceptionContinueThrowAsync<T>(Func<Task<T>> func, Action<Exception> onException)
        {
            return await CatchExceptionContinueThrowAsync<T, Exception>(func, onException);
        }

        public static void CatchExceptionContinueThrow(Action action, Action<Exception> onException)
        {
            CatchExceptionContinueThrow<Exception, ValueTuple>(action.ToFunc(), onException);
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
        /// Help to profiling an asyncTask.
        /// afterExecution is an optional action to execute. It's input is the task ElapsedMilliseconds of asyncTask execution.
        /// </summary>
        public static async Task ProfilingAsync(
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

        public static async Task WhenAll(params Task[] tasks)
        {
            await Task.WhenAll(tasks);
        }

        public static async Task WhenAll(IEnumerable<Task> tasks)
        {
            await Task.WhenAll(tasks);
        }

        public static async Task<IEnumerable<T>> WhenAll<T>(IEnumerable<Task<T>> tasks)
        {
            return await tasks.WhenAll();
        }

        public static async Task<ValueTuple<T1, T2>> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2)
        {
            await Task.WhenAll(task1, task2);
            return (task1.Result, task2.Result);
        }

        public static async Task<ValueTuple<T1, T2, T3>> WhenAll<T1, T2, T3>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3)
        {
            await Task.WhenAll(task1, task2, task3);
            return (task1.Result, task2.Result, task3.Result);
        }

        public static async Task<ValueTuple<T1, T2, T3, T4>> WhenAll<T1, T2, T3, T4>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4)
        {
            await Task.WhenAll(task1, task2, task3, task4);
            return (task1.Result, task2.Result, task3.Result, task4.Result);
        }

        public static async Task<ValueTuple<T1, T2, T3, T4, T5>> WhenAll<T1, T2, T3, T4, T5>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4,
            Task<T5> task5)
        {
            await Task.WhenAll(task1, task2, task3, task4, task5);
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result);
        }

        public static Task<T> Async<T>(T t)
        {
            return Task.FromResult(t);
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
        public static Task WaitRetryThrowFinalExceptionAsync<TException>(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalExceptionAsync(
                    executeFunc,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context})" />
        public static Task<T> WaitRetryThrowFinalExceptionAsync<T, TException>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null) where TException : Exception
        {
            return Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndThrowFinalExceptionAsync(
                    executeFunc,
                    onBeforeThrowFinalExceptionFn ?? (exception => { }));
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context})" />
        public static Task WaitRetryThrowFinalExceptionAsync(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return WaitRetryThrowFinalExceptionAsync<Exception>(executeFunc, sleepDurationProvider, retryCount, onBeforeThrowFinalExceptionFn, onRetry);
        }

        /// <inheritdoc cref="WaitRetryThrowFinalExceptionAsync{TException}(Func{Task},Func{int,TimeSpan},int,Action{Exception},Action{Exception,TimeSpan,int,Context})" />
        public static Task<T> WaitRetryThrowFinalExceptionAsync<T>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception> onBeforeThrowFinalExceptionFn = null,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return WaitRetryThrowFinalExceptionAsync<T, Exception>(executeFunc, sleepDurationProvider, retryCount, onBeforeThrowFinalExceptionFn, onRetry);
        }

        public static Task<PolicyResult> WaitRetryAsync(
            Func<Task> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCaptureAsync(
                    executeFunc);
        }

        public static Task<PolicyResult<T>> WaitRetryAsync<T>(
            Func<Task<T>> executeFunc,
            Func<int, TimeSpan> sleepDurationProvider = null,
            int retryCount = 1,
            Action<Exception, TimeSpan, int, Context> onRetry = null)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCaptureAsync(
                    executeFunc);
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
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
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
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
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
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
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
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
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
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
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
                    sleepDurationProvider ?? (retryAttempt => TimeSpan.FromSeconds(DefaultMinimumDelayWaitSeconds)),
                    onRetry ?? ((exception, timeSpan, currentRetry, context) => { }))
                .ExecuteAndCapture(
                    executeFunc);
        }

        public static void Wait(int millisecondsToWait)
        {
            Thread.Sleep(millisecondsToWait);
        }

        public static void DoThenWait(Action action, double secondsToWait = DefaultMinimumDelayWaitSeconds)
        {
            action();

            Thread.Sleep((int)(secondsToWait * 1000));
        }

        public static T DoThenWait<T>(Func<T> action, double secondsToWait = DefaultMinimumDelayWaitSeconds)
        {
            var result = action();

            Thread.Sleep((int)(secondsToWait * 1000));

            return result;
        }

        public static void WaitUntil(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            WaitUntilThen(condition, () => { }, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
        }

        public static void WaitUntilNoException(
            Func<bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            CatchException(
                () =>
                {
                    WaitUntilThen(condition, () => { }, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
                });
        }

        public static TResult WaitUntilGetResult<T, TResult>(
            T t,
            Func<T, TResult> waitResult,
            Func<TResult, bool> condition,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));

            while (!condition(waitResult(t)))
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilGetResult is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return waitResult(t);
        }

        public static T WaitUntil<T, TStopIfFailResult>(
            T t,
            Func<bool> condition,
            Func<T, TStopIfFailResult> stopWaitOnAssertError,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));
            while (!condition())
            {
                stopWaitOnAssertError(t);

                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilThen is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
            }

            return t;
        }

        public static TResult WaitUntilGetResult<T, TResult, TStopIfFailResult>(
            T t,
            Func<T, TResult> getResult,
            Func<TResult, bool> condition,
            Func<T, TStopIfFailResult> stopWaitOnAssertError,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));

            while (!condition(getResult(t)))
            {
                stopWaitOnAssertError(t);

                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilGetResult is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");
            }

            return getResult(t);
        }

        public static TResult WaitUntilNoException<T, TResult>(
            T t,
            Func<T, TResult> getResult,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds)
        {
            return WaitUntilNoException(t, getResult, _ => (TResult)default, maxWaitSeconds);
        }

        public static TResult WaitUntilNoException<T, TResult, TStopIfFailResult>(
            T t,
            Func<T, TResult> getResult,
            Func<T, TStopIfFailResult> stopWaitOnAssertError,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));

            while (true)
            {
                stopWaitOnAssertError(t);

                try
                {
                    return getResult(t);
                }
                catch (Exception)
                {
                    if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                        Thread.Sleep((int)(DefaultMinimumDelayWaitSeconds * 1000));
                    else
                        throw;
                }
            }
        }

        public static T WaitUntilThen<T>(
            Func<bool> condition,
            Func<T> action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(delayRetryTimeSeconds * 1000));
            while (!condition())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(delayRetryTimeSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilThen is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return action();
        }

        public static void WaitUntilThen(
            Func<bool> condition,
            Action action,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            WaitUntilThen(condition, action.ToFunc(), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
        }

        public static TTarget WaitUntilWhenThen<TSource, TTarget>(
            WhenCase<TSource, TTarget> whenDo,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            Thread.Sleep((int)(delayRetryTimeSeconds * 1000));
            while (!whenDo.HasMatchedCase())
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(delayRetryTimeSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"WaitUntilWhenThen is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

            return whenDo.Execute();
        }

        public static TSource WaitUntilWhenThen<TSource>(
            WhenCase<TSource> whenDo,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            WaitUntilWhenThen(whenDo.As<WhenCase<TSource, ValueTuple>>(), maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);

            return whenDo.Source;
        }

        public static void WaitAndRetryUntil(
            Action action,
            Func<bool> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            WaitAndRetryUntil(action.ToFunc(), until, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
        }

        public static T WaitAndRetryUntil<T>(
            Func<T> action,
            Func<bool> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            var result = action();

            while (!until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(delayRetryTimeSeconds * 1000));
                else
                    throw new TimeoutException(
                        $"DoUntil is timed out (Max: {maxWaitSeconds} seconds)." +
                        $"{(waitForMsg != null ? $"{Environment.NewLine}WaitFor: {waitForMsg}" : "")}");

                result = action();
            }

            return result;
        }

        public static async Task WaitAndRetryUntilAsync(
            Func<Task> action,
            Func<Task<bool>> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            await WaitAndRetryUntilAsync(action.ToAsyncFunc(), until, maxWaitSeconds, delayRetryTimeSeconds, waitForMsg);
        }

        public static async Task<T> WaitAndRetryUntilAsync<T>(
            Func<Task<T>> action,
            Func<Task<bool>> until,
            double maxWaitSeconds = DefaultWaitUntilMaxSeconds,
            double delayRetryTimeSeconds = DefaultMinimumDelayWaitSeconds,
            string waitForMsg = null)
        {
            var startWaitTime = DateTime.UtcNow;
            var maxWaitMilliseconds = maxWaitSeconds * 1000;

            var result = await action();

            while (!await until())
            {
                if ((DateTime.UtcNow - startWaitTime).TotalMilliseconds < maxWaitMilliseconds)
                    Thread.Sleep((int)(delayRetryTimeSeconds * 1000));
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
