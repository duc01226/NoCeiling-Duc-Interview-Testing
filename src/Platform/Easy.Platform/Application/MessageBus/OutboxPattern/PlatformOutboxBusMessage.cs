using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxBusMessage : RootEntity<PlatformOutboxBusMessage, string>, IRowVersionEntity
{
    public const int IdMaxLength = 200;
    public const int RoutingKeyMaxLength = 500;
    public const int MessageTypeFullNameMaxLength = 1000;
    public const double DefaultRetryProcessFailedMessageInSecondsUnit = 60;

    public string JsonMessage { get; set; }

    public string MessageTypeFullName { get; set; }

    public string RoutingKey { get; set; }

    public SendStatuses SendStatus { get; set; }

    public int? RetriedProcessCount { get; set; }

    public DateTime? NextRetryProcessAfter { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime LastSendDate { get; set; }

    public string LastSendError { get; set; }

    public Guid? ConcurrencyUpdateToken { get; set; }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> ToHandleOutboxEventBusMessagesExpr(
        double messageProcessingMaximumTimeInSeconds)
    {
        return p => p.SendStatus == SendStatuses.New ||
                    (p.SendStatus == SendStatuses.Failed &&
                     (p.NextRetryProcessAfter == null || p.NextRetryProcessAfter <= DateTime.UtcNow)) ||
                    (p.SendStatus == SendStatuses.Processing &&
                     p.LastSendDate <= Clock.UtcNow.AddSeconds(-messageProcessingMaximumTimeInSeconds));
    }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> ToCleanInboxEventBusMessagesExpr(
        double deleteProcessedMessageInSeconds,
        double deleteExpiredFailedMessageInSeconds)
    {
        return p => (p.LastSendDate <= Clock.UtcNow.AddSeconds(-deleteProcessedMessageInSeconds) &&
                     p.SendStatus == SendStatuses.Processed) ||
                    (p.LastSendDate <= Clock.UtcNow.AddSeconds(-deleteExpiredFailedMessageInSeconds) &&
                     p.SendStatus == SendStatuses.Failed);
    }

    public static PlatformOutboxBusMessage Create<TMessage>(
        TMessage message,
        string trackId,
        string routingKey,
        SendStatuses sendStatus,
        string lastSendError = null) where TMessage : class, new()
    {
        var nowDate = Clock.UtcNow;

        var result = new PlatformOutboxBusMessage
        {
            Id = BuildId(trackId),
            JsonMessage = message.ToFormattedJson(forceUseRuntimeType: true),
            MessageTypeFullName = GetMessageTypeFullName(message.GetType()),
            RoutingKey = routingKey.TakeTop(RoutingKeyMaxLength),
            LastSendDate = nowDate,
            CreatedDate = nowDate,
            SendStatus = sendStatus,
            LastSendError = lastSendError,
            RetriedProcessCount = lastSendError != null ? 1 : 0
        };

        return result;
    }

    public static string GetMessageTypeFullName(Type messageType)
    {
        return messageType.AssemblyQualifiedName.TakeTop(MessageTypeFullNameMaxLength);
    }

    public static string BuildId(string trackId)
    {
        return (trackId ?? Guid.NewGuid().ToString()).TakeTop(IdMaxLength);
    }

    public static DateTime CalculateNextRetryProcessAfter(
        int? retriedProcessCount,
        double retryProcessFailedMessageInSecondsUnit = DefaultRetryProcessFailedMessageInSecondsUnit)
    {
        return DateTime.UtcNow.AddSeconds(
            retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    }

    public enum SendStatuses
    {
        New,
        Processing,
        Processed,
        Failed
    }
}
