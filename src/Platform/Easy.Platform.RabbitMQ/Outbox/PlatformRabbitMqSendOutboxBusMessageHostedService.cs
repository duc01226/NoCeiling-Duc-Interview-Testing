using Easy.Platform.Application.Context;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.RabbitMQ.Outbox;

/// <summary>
/// <inheritdoc cref="PlatformSendOutboxBusMessageHostedService" />
/// </summary>
public sealed class PlatformRabbitMqSendOutboxBusMessageHostedService : PlatformSendOutboxBusMessageHostedService
{
    public PlatformRabbitMqSendOutboxBusMessageHostedService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(
        serviceProvider,
        loggerFactory,
        applicationSettingContext,
        outboxConfig)
    {
    }
}
