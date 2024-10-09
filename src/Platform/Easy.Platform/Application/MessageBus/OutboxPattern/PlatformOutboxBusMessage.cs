using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxBusMessage : RootEntity<PlatformOutboxBusMessage, string>, IRowVersionEntity
{
    public const int IdMaxLength = 400;
    public const int RoutingKeyMaxLength = 500;
    public const int MessageTypeFullNameMaxLength = 1000;
    public const double DefaultRetryProcessFailedMessageInSecondsUnit = 30;
    public const string BuildIdPrefixSeparator = "----";
    public const string BuildIdSubQueuePrefixSeparator = "++++";
    public const int CheckProcessingPingIntervalSeconds = 60;
    public const int MaxAllowedProcessingPingMisses = 30;

    public string JsonMessage { get; set; }

    public string MessageTypeFullName { get; set; }

    public string RoutingKey { get; set; }

    public SendStatuses SendStatus { get; set; }

    public int? RetriedProcessCount { get; set; }

    public DateTime? NextRetryProcessAfter { get; set; }

    public DateTime CreatedDate { get; set; } = Clock.UtcNow;

    public DateTime LastSendDate { get; set; }

    public DateTime? LastProcessingPingDate { get; set; }

    public string LastSendError { get; set; }

    public string? ConcurrencyUpdateToken { get; set; }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> CanHandleMessagesExpr()
    {
        return p => p.SendStatus == SendStatuses.New ||
                    (p.SendStatus == SendStatuses.Failed &&
                     (p.NextRetryProcessAfter == null || p.NextRetryProcessAfter <= DateTime.UtcNow)) ||
                    (p.SendStatus == SendStatuses.Processing &&
                     (p.LastProcessingPingDate == null ||
                      p.LastProcessingPingDate < Clock.UtcNow.AddSeconds(-CheckProcessingPingIntervalSeconds * MaxAllowedProcessingPingMisses)));
    }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> ToCleanExpiredMessagesExpr(
        double deleteProcessedMessageInSeconds,
        double deleteExpiredIgnoredMessageInSeconds)
    {
        return p => (p.CreatedDate <= Clock.UtcNow.AddSeconds(-deleteProcessedMessageInSeconds) &&
                     p.SendStatus == SendStatuses.Processed) ||
                    (p.CreatedDate <= Clock.UtcNow.AddSeconds(-deleteExpiredIgnoredMessageInSeconds) &&
                     p.SendStatus == SendStatuses.Ignored);
    }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> ToIgnoreFailedExpiredMessagesExpr(
        double ignoreExpiredFailedMessageInSeconds)
    {
        return p => p.CreatedDate <= Clock.UtcNow.AddSeconds(-ignoreExpiredFailedMessageInSeconds) &&
                    p.SendStatus == SendStatuses.Failed;
    }

    public static PlatformOutboxBusMessage Create<TMessage>(
        TMessage message,
        string trackId,
        string routingKey,
        SendStatuses sendStatus,
        string subQueueMessageIdPrefix,
        string lastSendError) where TMessage : class, new()
    {
        var nowDate = Clock.UtcNow;

        var result = new PlatformOutboxBusMessage
        {
            Id = BuildId(message.GetType(), trackId, subQueueMessageIdPrefix),
            JsonMessage = message.ToJson(forceUseRuntimeType: true),
            MessageTypeFullName = GetMessageTypeFullName(message.GetType()),
            RoutingKey = routingKey.TakeTop(RoutingKeyMaxLength),
            LastSendDate = nowDate,
            LastProcessingPingDate = nowDate,
            CreatedDate = nowDate,
            SendStatus = sendStatus,
            LastSendError = lastSendError,
            RetriedProcessCount = lastSendError != null ? 1 : 0
        };

        return result;
    }

    public static string GetMessageTypeFullName(Type messageType)
    {
        return messageType.AssemblyQualifiedName?.TakeTop(MessageTypeFullNameMaxLength);
    }

    public static string BuildId(Type messageType, string trackId, string subQueueMessageIdPrefix)
    {
        return $"{BuildIdPrefix(messageType, subQueueMessageIdPrefix)}{BuildIdPrefixSeparator}{trackId ?? Ulid.NewUlid().ToString()}".TakeTop(IdMaxLength);
    }

    public static string BuildIdPrefix(Type messageType, string subQueueMessageIdPrefix)
    {
        return $"{messageType.Name}{BuildIdSubQueuePrefixSeparator}{subQueueMessageIdPrefix}";
    }

    public static DateTime CalculateNextRetryProcessAfter(
        int? retriedProcessCount,
        double retryProcessFailedMessageInSecondsUnit = DefaultRetryProcessFailedMessageInSecondsUnit)
    {
        return DateTime.UtcNow.AddSeconds(
            retryProcessFailedMessageInSecondsUnit * retriedProcessCount ?? 1);
    }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
        Type messageType,
        string messageTrackId,
        DateTime messageCreatedDate,
        string subQueueMessageIdPrefix)
    {
        if (subQueueMessageIdPrefix.IsNullOrEmpty()) return p => false;

        return CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
            BuildIdPrefix(messageType, subQueueMessageIdPrefix),
            BuildId(messageType, messageTrackId, subQueueMessageIdPrefix),
            messageCreatedDate);
    }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
        PlatformOutboxBusMessage message)
    {
        if (GetSubQueuePrefix(message.Id).IsNullOrEmpty()) return p => false;

        return CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(message.GetIdPrefix(), message.Id, message.CreatedDate);
    }

    public static Expression<Func<PlatformOutboxBusMessage, bool>> CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
        string messageIdPrefix,
        string messageId,
        DateTime messageCreatedDate)
    {
        return p => p.Id.StartsWith(messageIdPrefix) &&
                    (p.SendStatus == SendStatuses.Failed || p.SendStatus == SendStatuses.Processing || p.SendStatus == SendStatuses.New) &&
                    p.Id != messageId &&
                    p.CreatedDate < messageCreatedDate;
    }

    public string GetIdPrefix()
    {
        return GetIdPrefix(Id);
    }

    public static string GetIdPrefix(string messageId)
    {
        var buildIdSeparatorIndex = messageId.IndexOf(BuildIdPrefixSeparator, StringComparison.Ordinal);

        return messageId.Substring(0, buildIdSeparatorIndex > 0 ? buildIdSeparatorIndex : messageId.Length);
    }

    public static string GetSubQueuePrefix(string messageId)
    {
        var buildIdSubQueueSeparatorIndex = messageId.IndexOf(BuildIdSubQueuePrefixSeparator, StringComparison.Ordinal);
        var buildIdSeparatorIndex = messageId.IndexOf(BuildIdPrefixSeparator, StringComparison.Ordinal);

        var subQueuePrefixStartIndex = buildIdSubQueueSeparatorIndex + BuildIdSubQueuePrefixSeparator.Length;

        return buildIdSubQueueSeparatorIndex >= 0 && buildIdSeparatorIndex > subQueuePrefixStartIndex
            ? messageId.Substring(subQueuePrefixStartIndex, buildIdSeparatorIndex - subQueuePrefixStartIndex)
            : null;
    }

    public enum SendStatuses
    {
        New,
        Processing,
        Processed,
        Failed,

        /// <summary>
        /// Ignored mean do not try to process this message anymore. Usually because it's failed, can't be processed but will still want to temporarily keep it
        /// </summary>
        Ignored
    }
}
