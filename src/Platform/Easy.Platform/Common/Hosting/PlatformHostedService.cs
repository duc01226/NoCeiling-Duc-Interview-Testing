using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Common.Hosting;

/// <summary>
/// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-6.0 <br />
/// It's usually used as a background service , will start on WebApplication.Run()
/// </summary>
public abstract class PlatformHostedService : IHostedService, IDisposable
{
    protected readonly SemaphoreSlim AsyncStartProcessLock = new SemaphoreSlim(1, 1);
    protected readonly SemaphoreSlim AsyncStopProcessLock = new SemaphoreSlim(1, 1);
    protected readonly ILogger Logger;

    protected bool ProcessStarted;
    protected bool ProcessStopped;
    protected readonly IServiceProvider ServiceProvider;

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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await AsyncStartProcessLock.WaitAsync(cancellationToken);

            if (ProcessStarted) return;

            await StartProcess(cancellationToken);

            ProcessStarted = true;

            Logger.LogInformation($"Process of {GetType().Name} Started");
        }
        finally
        {
            AsyncStartProcessLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await AsyncStopProcessLock.WaitAsync(cancellationToken);

            if (!ProcessStarted || ProcessStopped) return;

            await StopProcess(cancellationToken);

            ProcessStopped = true;

            Logger.LogInformation($"Process of {GetType().Name} Stopped");
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

    public static ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformHostedService));
    }
}
