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
    public const string BuildIdSeparator = "----";
    public const string BuildIdGroupedByConsumerPrefixSeparator = "_";

    private const double MarginRetryMessageProcessingMaximumTimeInSeconds = 60;

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

    public DateTime CreatedDate { get; set; } = Clock.UtcNow;

    public DateTime LastConsumeDate { get; set; }

    public DateTime? NextRetryProcessAfter { get; set; }

    public string LastConsumeError { get; set; }

    public Guid? ConcurrencyUpdateToken { get; set; }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CanHandleMessagesExpr(
        double messageProcessingMaximumTimeInSeconds)
    {
        return p => p.ConsumeStatus == ConsumeStatuses.New ||
                    (p.ConsumeStatus == ConsumeStatuses.Failed &&
                     (p.NextRetryProcessAfter == null || p.NextRetryProcessAfter <= DateTime.UtcNow)) ||
                    (p.ConsumeStatus == ConsumeStatuses.Processing &&
                     p.LastConsumeDate <= Clock.UtcNow.AddSeconds(-messageProcessingMaximumTimeInSeconds - MarginRetryMessageProcessingMaximumTimeInSeconds));
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> ToCleanExpiredMessagesByTimeExpr(
        double deleteProcessedMessageInSeconds,
        double deleteExpiredFailedMessageInSeconds)
    {
        return p => (p.LastConsumeDate <= Clock.UtcNow.AddSeconds(-deleteProcessedMessageInSeconds) &&
                     p.ConsumeStatus == ConsumeStatuses.Processed) ||
                    (p.LastConsumeDate <= Clock.UtcNow.AddSeconds(-deleteExpiredFailedMessageInSeconds) &&
                     p.ConsumeStatus == ConsumeStatuses.Failed);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(
        Type consumerType,
        string messageTrackId,
        DateTime messageCreatedDate,
        string extendedMessageIdPrefix)
    {
        return CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(
            BuildIdGroupedByConsumerPrefix(consumerType, extendedMessageIdPrefix),
            BuildId(consumerType, messageTrackId, extendedMessageIdPrefix),
            messageCreatedDate);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(
        PlatformInboxBusMessage message)
    {
        return CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(message.GetIdPrefix(), message.Id, message.CreatedDate);
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CheckAnySameConsumerOtherPreviousNotProcessedMessageExpr(
        string messageIdPrefix,
        string messageId,
        DateTime messageCreatedDate)
    {
        return p => p.Id.StartsWith(messageIdPrefix) &&
                    (p.ConsumeStatus == ConsumeStatuses.Failed || p.ConsumeStatus == ConsumeStatuses.Processing || p.ConsumeStatus == ConsumeStatuses.New) &&
                    p.Id != messageId &&
                    p.CreatedDate < messageCreatedDate;
    }

    public static string BuildId(Type consumerType, string trackId, string extendedMessageIdPrefix)
    {
        return $"{BuildIdGroupedByConsumerPrefix(consumerType, extendedMessageIdPrefix)}{BuildIdSeparator}{trackId ?? Guid.NewGuid().ToString()}".TakeTop(IdMaxLength);
    }

    public string GetIdPrefix()
    {
        return GetIdPrefix(Id);
    }

    public static string GetIdPrefix(string messageId)
    {
        return messageId.Substring(0, messageId.IndexOf(BuildIdSeparator, StringComparison.Ordinal));
    }

    public static string BuildIdGroupedByConsumerPrefix(Type consumerType, string extendedMessageIdPrefix)
    {
        return extendedMessageIdPrefix.IsNullOrEmpty() ? consumerType.Name : $"{consumerType.Name}{BuildIdGroupedByConsumerPrefixSeparator}{extendedMessageIdPrefix}";
    }

    public static DateTime CalculateNextRetryProcessAfter(
        int? retriedProcessCount,
        double retryProcessFailedMessageInSecondsUnit = DefaultRetryProcessFailedMessageInSecondsUnit)
    {
        return DateTime.UtcNow.AddSeconds(
            retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    }

    public static PlatformInboxBusMessage Create<TMessage>(
        TMessage message,
        string trackId,
        string produceFrom,
        string routingKey,
        Type consumerType,
        ConsumeStatuses consumeStatus,
        string extendedMessageIdPrefix,
        string lastConsumeError) where TMessage : class
    {
        var nowDate = Clock.UtcNow;

        var result = new PlatformInboxBusMessage
        {
            Id = BuildId(consumerType, trackId, extendedMessageIdPrefix),
            JsonMessage = message.ToFormattedJson(),
            MessageTypeFullName = message.GetType().FullName?.TakeTop(MessageTypeFullNameMaxLength),
            ProduceFrom = produceFrom,
            RoutingKey = routingKey.TakeTop(RoutingKeyMaxLength),
            LastConsumeDate = nowDate,
            CreatedDate = nowDate,
            ConsumerBy = GetConsumerByValue(consumerType),
            ConsumeStatus = consumeStatus,
            LastConsumeError = lastConsumeError,
            RetriedProcessCount = lastConsumeError != null ? 1 : 0
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
        Failed
    }
}
