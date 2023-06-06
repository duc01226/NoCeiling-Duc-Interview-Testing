namespace Easy.Platform.Common.Extensions;

public static class ThreadExtensions
{
    public static void ExecuteLockAction(this SemaphoreSlim lockObj, Action action)
    {
        try
        {
            lockObj.Wait();

            action();
        }
        finally
        {
            lockObj.Release();
        }
    }

    public static T ExecuteLockAction<T>(this SemaphoreSlim lockObj, Func<T> action)
    {
        try
        {
            lockObj.Wait();

            return action();
        }
        finally
        {
            lockObj.Release();
        }
    }

    public static async Task ExecuteLockActionAsync(this SemaphoreSlim lockObj, Func<Task> action, CancellationToken cancellationToken = default)
    {
        try
        {
            await lockObj.WaitAsync(cancellationToken);

            await action();
        }
        finally
        {
            lockObj.Release();
        }
    }

    public static async Task<T> ExecuteLockActionAsync<T>(this SemaphoreSlim lockObj, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        try
        {
            await lockObj.WaitAsync(cancellationToken);

            return await action();
        }
        finally
        {
            lockObj.Release();
        }
    }
}
