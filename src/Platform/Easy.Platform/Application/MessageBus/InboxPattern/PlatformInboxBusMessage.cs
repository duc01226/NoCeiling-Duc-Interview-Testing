using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxBusMessage : RootEntity<PlatformInboxBusMessage, string>, IRowVersionEntity
{
    public const int IdMaxLength = 400;
    public const int MessageTypeFullNameMaxLength = 1000;
    public const int RoutingKeyMaxLength = 500;
    public const double DefaultRetryProcessFailedMessageInSecondsUnit = 60;
    public const string BuildIdPrefixSeparator = "----";
    public const string BuildIdSubQueuePrefixSeparator = "++++";
    public const int CheckProcessingPingIntervalSeconds = 30;
    public const int MaxAllowedProcessingPingMisses = 10;

    public string JsonMessage { get; set; }

    public string MessageTypeFullName { get; set; }

    public string ProduceFrom { get; set; }

    public string RoutingKey { get; set; }

    /// <summary>
    /// Consumer Type FullName
    /// </summary>
    public string ConsumerBy { get; set; }

    public ConsumeStatuses ConsumeStatus { get; set; }

    public int? RetriedProcessCount { get; set; }

    public string? ForApplicationName { get; set; }

    public DateTime CreatedDate { get; set; } = Clock.UtcNow;

    public DateTime LastConsumeDate { get; set; }

    public DateTime? LastProcessingPingDate { get; set; }

    public DateTime? NextRetryProcessAfter { get; set; }

    public string LastConsumeError { get; set; }

    public string? ConcurrencyUpdateToken { get; set; }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CanHandleMessagesExpr(
        string forApplicationName,
        int maxRetriedProcessCount,
        bool retryFailedMessageImmediately = false,
        DateTime? firstTimeProcessDate = null)
    {
        Expression<Func<PlatformInboxBusMessage, bool>> initialExpr =
            p => p.ConsumeStatus == ConsumeStatuses.New ||
                 (p.ConsumeStatus == ConsumeStatuses.Failed && ((retryFailedMessageImmediately && p.LastConsumeDate < firstTimeProcessDate) ||
                                                                (p.RetriedProcessCount <= maxRetriedProcessCount &&
                                                                 (p.NextRetryProcessAfter == null ||
                                                                  p.NextRetryProcessAfter <= DateTime.UtcNow)))) ||
                 (p.ConsumeStatus == ConsumeStatuses.Processing &&
                  (p.LastProcessingPingDate == null ||
                   p.LastProcessingPingDate < Clock.UtcNow.AddSeconds(-CheckProcessingPingIntervalSeconds * MaxAllowedProcessingPingMisses)));

        return initialExpr.AndAlsoIf(forApplicationName.IsNotNullOrEmpty(), () => p => p.ForApplicationName == null || p.ForApplicationName == forApplicationName);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> ToCleanExpiredMessagesExpr(
        double deleteProcessedMessageInSeconds,
        double deleteExpiredIgnoredMessageInSeconds)
    {
        return p => (p.CreatedDate <= Clock.UtcNow.AddSeconds(-deleteProcessedMessageInSeconds) &&
                     p.ConsumeStatus == ConsumeStatuses.Processed) ||
                    (p.CreatedDate <= Clock.UtcNow.AddSeconds(-deleteExpiredIgnoredMessageInSeconds) &&
                     p.ConsumeStatus == ConsumeStatuses.Ignored);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> ToIgnoreFailedExpiredMessagesExpr(
        double ignoreExpiredFailedMessageInSeconds,
        int maxRetriedProcessCount)
    {
        return p => p.ConsumeStatus == ConsumeStatuses.Failed &&
                    (p.CreatedDate <= Clock.UtcNow.AddSeconds(-ignoreExpiredFailedMessageInSeconds) || p.RetriedProcessCount > maxRetriedProcessCount);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
        Type consumerType,
        string messageTrackId,
        DateTime messageCreatedDate,
        string subQueueMessageIdPrefix)
    {
        if (subQueueMessageIdPrefix.IsNullOrEmpty()) return p => false;

        return CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
            BuildIdPrefix(consumerType, subQueueMessageIdPrefix),
            BuildId(consumerType, messageTrackId, subQueueMessageIdPrefix),
            messageCreatedDate);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
        PlatformInboxBusMessage message)
    {
        if (GetSubQueuePrefix(message.Id).IsNullOrEmpty()) return p => false;

        return CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(message.GetIdPrefix(), message.Id, message.CreatedDate);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CheckAnySameSubQueueMessageIdPrefixOtherPreviousNotProcessedMessageExpr(
        string messageIdPrefix,
        string messageId,
        DateTime messageCreatedDate)
    {
        return p => p.Id.StartsWith(messageIdPrefix) &&
                    (p.ConsumeStatus == ConsumeStatuses.Failed || p.ConsumeStatus == ConsumeStatuses.Processing || p.ConsumeStatus == ConsumeStatuses.New) &&
                    p.Id != messageId &&
                    p.CreatedDate < messageCreatedDate;
    }

    public static string BuildId(Type consumerType, string trackId, string subQueueMessageIdPrefix)
    {
        return $"{BuildIdPrefix(consumerType, subQueueMessageIdPrefix)}{BuildIdPrefixSeparator}{trackId ?? Ulid.NewUlid().ToString()}".TakeTop(IdMaxLength);
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

    public static string BuildIdPrefix(Type consumerType, string subQueueMessageIdPrefix)
    {
        return
            $"{consumerType.GetNameOrGenericTypeName()}{BuildIdSubQueuePrefixSeparator}{subQueueMessageIdPrefix}";
    }

    public static DateTime CalculateNextRetryProcessAfter(
        int? retriedProcessCount,
        double retryProcessFailedMessageInSecondsUnit = DefaultRetryProcessFailedMessageInSecondsUnit)
    {
        return DateTime.UtcNow.AddSeconds(
            retryProcessFailedMessageInSecondsUnit * Math.Pow(retriedProcessCount ?? 1, 2));
    }

    public static PlatformInboxBusMessage Create<TMessage>(
        TMessage message,
        string trackId,
        string produceFrom,
        string routingKey,
        Type consumerType,
        ConsumeStatuses consumeStatus,
        string forApplicationName,
        string subQueueMessageIdPrefix) where TMessage : class
    {
        var nowDate = Clock.UtcNow;

        var result = new PlatformInboxBusMessage
        {
            Id = BuildId(consumerType, trackId, subQueueMessageIdPrefix),
            JsonMessage = message.ToJson(),
            MessageTypeFullName = message.GetType().FullName?.TakeTop(MessageTypeFullNameMaxLength),
            ProduceFrom = produceFrom,
            RoutingKey = routingKey.TakeTop(RoutingKeyMaxLength),
            LastConsumeDate = nowDate,
            CreatedDate = nowDate,
            ConsumerBy = GetConsumerByValue(consumerType),
            ConsumeStatus = consumeStatus,
            RetriedProcessCount = 0,
            ForApplicationName = forApplicationName,
            LastProcessingPingDate = Clock.UtcNow
        };

        return result;
    }

    public static string GetConsumerByValue(Type consumerType)
    {
        return consumerType.FullName;
    }

    public enum ConsumeStatuses
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
