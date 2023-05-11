using Polly.Retry;

namespace Easy.Platform.Common.Extensions;

public static class RetryPolicyExtension
{
    public static void ExecuteAndThrowFinalException(
        this RetryPolicy retryPolicy,
        Action action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        // Store stack trace before call retryPolicy.ExecuteAsync to keep the original stack trace to log
        // after retryPolicy.ExecuteAsync will lose full stack trace (may because of the library issues)
        var stackTrace = Environment.StackTrace;

        try
        {
            retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw new Exception($"{e.Message}. FullStackTrace: {stackTrace}", e);
        }
    }

    public static T ExecuteAndThrowFinalException<T>(
        this RetryPolicy retryPolicy,
        Func<T> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        // Store stack trace before call retryPolicy.ExecuteAsync to keep the original stack trace to log
        // after retryPolicy.ExecuteAsync will lose full stack trace (may because of the library issues)
        var stackTrace = Environment.StackTrace;

        try
        {
            return retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw new Exception($"{e.Message}. FullStackTrace: {stackTrace}", e);
        }
    }

    public static void ExecuteAndThrowFinalException<TException>(
        this RetryPolicy retryPolicy,
        Action action,
        Action<TException> onBeforeThrowFinalExceptionFn = null) where TException : Exception
    {
        // Store stack trace before call retryPolicy.ExecuteAsync to keep the original stack trace to log
        // after retryPolicy.ExecuteAsync will lose full stack trace (may because of the library issues)
        var stackTrace = Environment.StackTrace;

        try
        {
            retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            if (e.As<TException>() != null) onBeforeThrowFinalExceptionFn?.Invoke(e.As<TException>());

            throw new Exception($"{e.Message}. FullStackTrace: {stackTrace}", e);
        }
    }

    public static T ExecuteAndThrowFinalException<T, TException>(
        this RetryPolicy retryPolicy,
        Func<T> action,
        Action<TException> onBeforeThrowFinalExceptionFn = null) where TException : Exception
    {
        // Store stack trace before call retryPolicy.ExecuteAsync to keep the original stack trace to log
        // after retryPolicy.ExecuteAsync will lose full stack trace (may because of the library issues)
        var stackTrace = Environment.StackTrace;

        try
        {
            return retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            if (e.As<TException>() != null) onBeforeThrowFinalExceptionFn?.Invoke(e.As<TException>());

            throw new Exception($"{e.Message}. FullStackTrace: {stackTrace}", e);
        }
    }

    public static async Task ExecuteAndThrowFinalExceptionAsync(
        this AsyncRetryPolicy retryPolicy,
        Func<Task> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        // Store stack trace before call retryPolicy.ExecuteAsync to keep the original stack trace to log
        // after retryPolicy.ExecuteAsync will lose full stack trace (may because of the library issues)
        var stackTrace = Environment.StackTrace;

        try
        {
            await retryPolicy.ExecuteAsync(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw new Exception($"{e.Message}. FullStackTrace: {stackTrace}", e);
        }
    }

    public static async Task<T> ExecuteAndThrowFinalExceptionAsync<T>(
        this AsyncRetryPolicy retryPolicy,
        Func<Task<T>> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        // Store stack trace before call retryPolicy.ExecuteAsync to keep the original stack trace to log
        // after retryPolicy.ExecuteAsync will lose full stack trace (may because of the library issues)
        var stackTrace = Environment.StackTrace;

        try
        {
            return await retryPolicy.ExecuteAsync(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);

            throw new Exception($"{e.Message}. FullStackTrace: {stackTrace}", e);
        }
    }
}
