using Easy.Platform.Common;
using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireBackgroundJobProcessingService : IPlatformBackgroundJobProcessingService, IDisposable
{
    public static readonly long WaitForShutdownTimeoutInSeconds = 5 * 60;

    private BackgroundJobServer currentBackgroundJobServer;

    private readonly BackgroundJobServerOptions options;

    public PlatformHangfireBackgroundJobProcessingService(BackgroundJobServerOptions options)
    {
        this.options = options;
        Logger = CreateLogger();
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
        Logger.LogInformation($"{GetType().Name} STARTED");

        currentBackgroundJobServer ??= new BackgroundJobServer(options);

        Logger.LogInformation($"{GetType().Name} FINISHED");
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

    public static ILogger CreateLogger()
    {
        return PlatformGlobal.LoggerFactory.CreateLogger(typeof(PlatformHangfireBackgroundJobProcessingService));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) currentBackgroundJobServer?.Dispose();
    }
}
