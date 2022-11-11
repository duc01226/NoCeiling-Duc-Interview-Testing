using System.Threading;
using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;

namespace Easy.Platform.HangfireBackgroundJob;

public sealed class PlatformHangfireBackgroundJobProcessingService : IPlatformBackgroundJobProcessingService, IDisposable
{
    public static readonly long WaitForShutdownTimeoutInSeconds = 5 * 60;

    private readonly BackgroundJobServerOptions options;

    private BackgroundJobServer currentBackgroundJobServer;

    public PlatformHangfireBackgroundJobProcessingService(BackgroundJobServerOptions options)
    {
        this.options = options;
    }

    public void Dispose()
    {
        currentBackgroundJobServer?.Dispose();
    }

    public bool Started()
    {
        return currentBackgroundJobServer != null;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        currentBackgroundJobServer ??= new BackgroundJobServer(options);
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
}
