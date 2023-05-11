using Easy.Platform.Application.Context;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.RabbitMQ.Inbox;

/// <summary>
/// <inheritdoc cref="PlatformConsumeInboxBusMessageHostedService" />
/// </summary>
public class PlatformRabbitMqConsumeInboxBusMessageHostedService : PlatformConsumeInboxBusMessageHostedService
{
    private readonly PlatformRabbitMqOptions options;

    public PlatformRabbitMqConsumeInboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerBuilder,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformRabbitMqOptions options,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformInboxConfig inboxConfig) : base(
        serviceProvider,
        loggerBuilder,
        applicationSettingContext,
        messageBusScanner,
        inboxConfig)
    {
        this.options = options;
    }

    protected override bool IsLogConsumerProcessTime()
    {
        return options.IsLogConsumerProcessTime;
    }

    protected override double LogErrorSlowProcessWarningTimeMilliseconds()
    {
        return options.LogErrorSlowProcessWarningTimeMilliseconds;
    }
}
