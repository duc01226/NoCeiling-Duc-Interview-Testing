using Easy.Platform.Common.HostingBackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.HostingBackgroundServices;

public abstract class PlatformApplicationIntervalHostingBackgroundService : PlatformIntervalHostingBackgroundService
{
    protected PlatformApplicationIntervalHostingBackgroundService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
        ApplicationSettingContext = serviceProvider.GetRequiredService<IPlatformApplicationSettingContext>();
    }

    public override bool LogIntervalProcessInformation => ApplicationSettingContext.IsDebugInformationMode;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }
}
