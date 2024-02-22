using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireBackgroundJobProcessingService : IPlatformBackgroundJobProcessingService, IDisposable
{
    public static readonly long WaitForShutdownTimeoutInSeconds = 5 * 60;

    private readonly BackgroundJobServerOptions options;

    private BackgroundJobServer currentBackgroundJobServer;
    private bool disposed;

    public PlatformHangfireBackgroundJobProcessingService(BackgroundJobServerOptions options, ILoggerFactory loggerFactory)
    {
        this.options = options;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    protected ILogger Logger { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool Started()
    {
        return currentBackgroundJobServer != null;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("{TargetName} STARTED", GetType().Name);

        currentBackgroundJobServer ??= new BackgroundJobServer(options);

        Logger.LogInformation("{TargetName} FINISHED", GetType().Name);
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        if (currentBackgroundJobServer != null)
        {
            currentBackgroundJobServer.SendStop();
            currentBackgroundJobServer.WaitForShutdown(TimeSpan.FromSeconds(WaitForShutdownTimeoutInSeconds));
            currentBackgroundJobServer.Dispose();
            currentBackgroundJobServer = null;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
                // Release managed resources
                currentBackgroundJobServer?.Dispose();

            // Release unmanaged resources

            disposed = true;
        }
    }

    ~PlatformHangfireBackgroundJobProcessingService()
    {
        Dispose(false);
    }
}
