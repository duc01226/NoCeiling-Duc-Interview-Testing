using Easy.Platform.Application;
using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.HangfireBackgroundJob;

public sealed class PlatformHangfireBackgroundJobProcessingService : IPlatformBackgroundJobProcessingService, IDisposable
{
    public static readonly long WaitForShutdownTimeoutInSeconds = 5 * 60;

    private BackgroundJobServer currentBackgroundJobServer;

    private readonly BackgroundJobServerOptions options;

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
        PlatformApplicationGlobal.LoggerFactory.CreateLogger(GetType()).LogInformation($"{GetType().Name} starting");

        currentBackgroundJobServer ??= new BackgroundJobServer(options);

        PlatformApplicationGlobal.LoggerFactory.CreateLogger(GetType()).LogInformation($"{GetType().Name} started");
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
