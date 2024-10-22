using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.HostingBackgroundServices;

/// <summary>
/// Abstract base class for implementing a hosted service in an ASP.NET Core application.
/// <br />
/// It's usually used as a background service, and will start when WebHost.Run is called.
/// </summary>
/// <remarks>
/// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-6.0 <br />
/// It's usually used as a background service , will start on WebApplication.Run()
/// </remarks>
public abstract class PlatformHostingBackgroundService : IHostedService, IDisposable
{
    /// <summary>
    /// SemaphoreSlim to control asynchronous start process.
    /// </summary>
    protected readonly SemaphoreSlim AsyncStartProcessLock = new(1, 1);

    /// <summary>
    /// SemaphoreSlim to control asynchronous stop process.
    /// </summary>
    protected readonly SemaphoreSlim AsyncStopProcessLock = new(1, 1);

    /// <summary>
    /// Logger instance for logging information and messages.
    /// </summary>
    protected readonly ILogger Logger;

    protected readonly ILoggerFactory LoggerFactory;

    /// <summary>
    /// Service provider for resolving dependencies.
    /// </summary>
    protected readonly IServiceProvider ServiceProvider;

    /// <summary>
    /// Task representing the execution process.
    /// </summary>
    protected Task ExecuteTask;

    /// <summary>
    /// Flag indicating whether the process has started.
    /// </summary>
    protected bool ProcessStarted;

    /// <summary>
    /// Flag indicating whether the process has stopped.
    /// </summary>
    protected bool ProcessStopped;

    /// <summary>
    /// CancellationTokenSource for stopping the execution process.
    /// </summary>
    protected CancellationTokenSource StoppingCts;

    /// <summary>
    /// Flag indicating whether the object has been disposed.
    /// </summary>
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformHostingBackgroundService" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="loggerFactory">The logger factory for creating the logger instance.</param>
    public PlatformHostingBackgroundService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        ServiceProvider = serviceProvider;
        Logger = CreateLogger(loggerFactory);
        LoggerFactory = loggerFactory;
    }

    /// <summary>
    /// Disposes of the object, releasing both managed and unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Starts the hosted service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the start operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            AsyncStartProcessLock.Wait(cancellationToken);

            if (ProcessStarted) return Task.CompletedTask;

            Logger.LogInformation("[PlatformHostingBackgroundService] {TargetName} STARTED", GetType().Name);

            // Create linked token to allow cancelling executing task from provided token
            StoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Store the task we're executing
            ExecuteTask = StartProcess(cancellationToken);

            ProcessStarted = true;

            Logger.LogInformation("[PlatformHostingBackgroundService] {TargetName} FINISHED", GetType().Name);

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

    /// <summary>
    /// Stops the hosted service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the stop operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop called without start
        if (ExecuteTask == null) return;

        try
        {
            await AsyncStopProcessLock.WaitAsync(cancellationToken);

            if (!ProcessStarted || ProcessStopped) return;

            // Signal cancellation to the executing method
            if (StoppingCts != null) await StoppingCts.CancelAsync();

            await StopProcess(cancellationToken);

            ProcessStopped = true;

            Logger.LogInformation("[PlatformHostingBackgroundService] Process of {TargetName} Stopped", GetType().Name);
        }
        finally
        {
            AsyncStopProcessLock.Release();
        }
    }

    /// <summary>
    /// Finalizer for the <see cref="PlatformHostingBackgroundService" /> class.
    /// </summary>
    ~PlatformHostingBackgroundService()
    {
        Dispose(false);
    }

    /// <summary>
    /// Starts the execution process of the hosted service.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    protected abstract Task StartProcess(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the execution process of the hosted service.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    protected virtual Task StopProcess(CancellationToken cancellationToken) { return Task.CompletedTask; }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    protected virtual void DisposeManagedResource()
    {
        AsyncStartProcessLock?.Dispose();
        AsyncStopProcessLock?.Dispose();
    }

    /// <summary>
    /// Creates a logger instance for the hosted service.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating the logger instance.</param>
    /// <returns>The created logger instance.</returns>
    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Disposes of the object, releasing both managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is called from the <see cref="Dispose" /> method.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing) DisposeManagedResource();

            // Release unmanaged resources

            disposed = true;
        }
    }
}
