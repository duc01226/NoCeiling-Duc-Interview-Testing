using Easy.Platform.Application.HostingBackgroundServices;
using Microsoft.Extensions.Logging;

namespace PlatformExampleApp.TextSnippet.Application.HostingBackgroundServices;

internal sealed class ExampleHostingBackgroundService : PlatformApplicationIntervalHostingBackgroundService
{
    public ExampleHostingBackgroundService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    protected override Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
