using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Hosting;

/// <summary>
/// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-6.0 <br />
/// It's usually used as a background service , will start on WebApplication.Run()
/// </summary>
public abstract class PlatformHostedService : IHostedService, IDisposable
{
    protected readonly SemaphoreSlim AsyncStartProcessLock = new(1, 1);
    protected readonly SemaphoreSlim AsyncStopProcessLock = new(1, 1);
    protected readonly ILogger Logger;
    protected readonly IServiceProvider ServiceProvider;
    protected Task ExecuteTask;

    protected bool ProcessStarted;
    protected bool ProcessStopped;
    protected CancellationTokenSource StoppingCts;

    public PlatformHostedService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        ServiceProvider = serviceProvider;
        Logger = CreateLogger(loggerFactory);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            AsyncStartProcessLock.Wait(cancellationToken);

            if (ProcessStarted) return Task.CompletedTask;

            Logger.LogInformation("HostedService {TargetName} Start STARTED", GetType().Name);

            // Create linked token to allow cancelling executing task from provided token
            StoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Store the task we're executing
            ExecuteTask = StartProcess(cancellationToken);

            ProcessStarted = true;

            Logger.LogInformation("HostedService {TargetName} Start FINISHED", GetType().Name);

            // If the task is completed then return it, this will bubble cancellation and failure to the caller
            if (ExecuteTask.IsCompleted) return ExecuteTask;

            // Otherwise it's running
            return Task.CompletedTask;
        }
        finally
        {
            AsyncStartProcessLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop called without start
        if (ExecuteTask == null) return;

        try
        {
            await AsyncStopProcessLock.WaitAsync(cancellationToken);

            if (!ProcessStarted || ProcessStopped) return;

            // Signal cancellation to the executing method
            StoppingCts!.Cancel();

            await StopProcess(cancellationToken);

            ProcessStopped = true;

            Logger.LogInformation("Process of {TargetName} Stopped", GetType().Name);
        }
        finally
        {
            AsyncStopProcessLock.Release();
        }
    }

    protected abstract Task StartProcess(CancellationToken cancellationToken);
    protected virtual Task StopProcess(CancellationToken cancellationToken) { return Task.CompletedTask; }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            DisposeManagedResource();
    }

    protected virtual void DisposeManagedResource()
    {
        AsyncStartProcessLock?.Dispose();
        AsyncStopProcessLock?.Dispose();
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformHostedService));
    }
}
