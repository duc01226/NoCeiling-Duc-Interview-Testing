using Easy.Platform.Application.Context;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.RabbitMQ.Outbox;

/// <summary>
/// <inheritdoc cref="PlatformSendOutboxBusMessageHostedService" />
/// </summary>
public class PlatformRabbitMqSendOutboxBusMessageHostedService : PlatformSendOutboxBusMessageHostedService
{
    public PlatformRabbitMqSendOutboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerBuilder,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(
        serviceProvider,
        loggerBuilder,
        applicationSettingContext,
        outboxConfig)
    {
    }
}
