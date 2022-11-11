using Polly.Retry;

namespace Easy.Platform.Common.Extensions;

public static class RetryPolicyExtension
{
    public static void ExecuteAndThrowFinalException(
        this RetryPolicy retryPolicy,
        Action action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        try
        {
            retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);
            throw;
        }
    }

    public static T ExecuteAndThrowFinalException<T>(
        this RetryPolicy retryPolicy,
        Func<T> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        try
        {
            return retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);
            throw;
        }
    }

    public static void ExecuteAndThrowFinalException<TException>(
        this RetryPolicy retryPolicy,
        Action action,
        Action<TException> onBeforeThrowFinalExceptionFn = null) where TException : Exception
    {
        try
        {
            retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            if (e.As<TException>() != null) onBeforeThrowFinalExceptionFn?.Invoke(e.As<TException>());
            throw;
        }
    }

    public static T ExecuteAndThrowFinalException<T, TException>(
        this RetryPolicy retryPolicy,
        Func<T> action,
        Action<TException> onBeforeThrowFinalExceptionFn = null) where TException : Exception
    {
        try
        {
            return retryPolicy.Execute(action);
        }
        catch (Exception e)
        {
            if (e.As<TException>() != null) onBeforeThrowFinalExceptionFn?.Invoke(e.As<TException>());
            throw;
        }
    }

    public static async Task ExecuteAndThrowFinalExceptionAsync(
        this AsyncRetryPolicy retryPolicy,
        Func<Task> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        try
        {
            await retryPolicy.ExecuteAsync(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);
            throw;
        }
    }

    public static async Task<T> ExecuteAndThrowFinalExceptionAsync<T>(
        this AsyncRetryPolicy retryPolicy,
        Func<Task<T>> action,
        Action<Exception> onBeforeThrowFinalExceptionFn = null)
    {
        try
        {
            return await retryPolicy.ExecuteAsync(action);
        }
        catch (Exception e)
        {
            onBeforeThrowFinalExceptionFn?.Invoke(e);
            throw;
        }
    }
}
