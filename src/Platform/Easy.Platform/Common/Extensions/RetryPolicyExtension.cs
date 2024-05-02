using Easy.Platform.Common.Utils;
using Polly.Retry;

namespace Easy.Platform.Common.Extensions;

public static class RetryPolicyExtension
{
    /// <summary>
    /// Executes the specified action within the retry policy. If an exception occurs, it invokes the provided callback function before rethrowing the exception.
    /// </summary>
    /// <param name="retryPolicy">The retry policy within which the action is to be executed.</param>
    /// <param name="action">The action to be executed.</param>
    /// <param name="onBeforeThrowFinalExceptionFn">An optional callback function to be invoked before rethrowing the exception. Default is null.</param>
    /// <exception cref="Exception">The exception that was caught during the execution of the action.</exception>
    public static void ExecuteAndThrowFinalException(
        this RetryPolicy retryPolicy,
        Action action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        try
        {
            retryPolicy.Execute(
                () =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                });
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw;
        }
    }

    /// <summary>
    /// Executes the specified function within the retry policy and returns the result. If an exception occurs, it invokes the provided callback function before rethrowing the exception.
    /// </summary>
    /// <typeparam name="T">The type of the return value of the function.</typeparam>
    /// <param name="retryPolicy">The retry policy within which the function is to be executed.</param>
    /// <param name="action">The function to be executed.</param>
    /// <param name="onBeforeThrowFinalExceptionFn">An optional callback function to be invoked before rethrowing the exception. Default is null.</param>
    /// <returns>The result of the function execution.</returns>
    /// <exception cref="Exception">The exception that was caught during the execution of the function.</exception>
    public static T ExecuteAndThrowFinalException<T>(
        this RetryPolicy retryPolicy,
        Func<T> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        try
        {
            return retryPolicy.Execute(
                () =>
                {
                    try
                    {
                        return action();
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                });
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw;
        }
    }

    /// <summary>
    /// Executes the specified action within the retry policy. If an exception of type TException occurs, it invokes the provided callback function before rethrowing the exception.
    /// </summary>
    /// <typeparam name="TException">The type of the exception to be caught.</typeparam>
    /// <param name="retryPolicy">The retry policy within which the action is to be executed.</param>
    /// <param name="action">The action to be executed.</param>
    /// <param name="onBeforeThrowFinalExceptionFn">An optional callback function to be invoked before rethrowing the exception. Default is null.</param>
    public static void ExecuteAndThrowFinalException<TException>(
        this RetryPolicy retryPolicy,
        Action action,
        Action<TException> onBeforeThrowFinalExceptionFn = null) where TException : Exception
    {
        try
        {
            retryPolicy.Execute(
                () =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                });
        }
        catch (Exception e)
        {
            if (e.As<TException>() != null) onBeforeThrowFinalExceptionFn?.Invoke(e.As<TException>());

            throw;
        }
    }

    /// <summary>
    /// Executes the specified function within the retry policy and returns the result. If an exception of type TException occurs, it invokes the provided callback function before rethrowing the exception.
    /// </summary>
    /// <typeparam name="T">The type of the return value of the function.</typeparam>
    /// <typeparam name="TException">The type of the exception to be caught.</typeparam>
    /// <param name="retryPolicy">The retry policy within which the function is to be executed.</param>
    /// <param name="action">The function to be executed.</param>
    /// <param name="onBeforeThrowFinalExceptionFn">An optional callback function to be invoked before rethrowing the exception. Default is null.</param>
    /// <returns>The result of the function execution.</returns>
    public static T ExecuteAndThrowFinalException<T, TException>(
        this RetryPolicy retryPolicy,
        Func<T> action,
        Action<TException> onBeforeThrowFinalExceptionFn = null) where TException : Exception
    {
        try
        {
            return retryPolicy.Execute(
                () =>
                {
                    try
                    {
                        return action();
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                });
        }
        catch (Exception e)
        {
            if (e.As<TException>() != null) onBeforeThrowFinalExceptionFn?.Invoke(e.As<TException>());

            throw;
        }
    }

    /// <summary>
    /// Executes the specified asynchronous function within the retry policy. If an exception occurs, it invokes the provided callback function before rethrowing the exception.
    /// </summary>
    /// <param name="retryPolicy">The retry policy within which the function is to be executed.</param>
    /// <param name="action">The asynchronous function to be executed.</param>
    /// <param name="onBeforeThrowFinalExceptionFn">An optional callback function to be invoked before rethrowing the exception. Default is null.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exception">The exception that was caught during the execution of the function.</exception>
    public static async Task ExecuteAndThrowFinalExceptionAsync(
        this AsyncRetryPolicy retryPolicy,
        Func<Task> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await retryPolicy.ExecuteAsync(
                async ct =>
                {
                    try
                    {
                        await action();
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                },
                cancellationToken);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw;
        }
    }

    /// <summary>
    /// Executes the specified asynchronous function within the retry policy and returns the result. If an exception occurs, it invokes the provided callback function before rethrowing the exception.
    /// </summary>
    /// <typeparam name="T">The type of the return value of the function.</typeparam>
    /// <param name="retryPolicy">The retry policy within which the function is to be executed.</param>
    /// <param name="action">The asynchronous function to be executed.</param>
    /// <param name="onBeforeThrowFinalExceptionFn">An optional callback function to be invoked before rethrowing the exception. Default is null.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous operation. The task result is the result of the function execution.</returns>
    /// <exception cref="Exception">The exception that was caught during the execution of the function.</exception>
    public static async Task<T> ExecuteAndThrowFinalExceptionAsync<T>(
        this AsyncRetryPolicy retryPolicy,
        Func<Task<T>> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await retryPolicy.ExecuteAsync(
                async ct =>
                {
                    try
                    {
                        return await action();
                    }
                    finally
                    {
                        Util.GarbageCollector.Collect();
                    }
                },
                cancellationToken);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw;
        }
    }
}
