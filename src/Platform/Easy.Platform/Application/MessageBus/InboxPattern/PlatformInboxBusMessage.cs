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
    public const double DefaultRetryProcessFailedMessageInSecondsUnit = 30;
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

    public string? ForApplicationName { get; set; }

    public DateTime CreatedDate { get; set; } = Clock.UtcNow;

    public DateTime LastConsumeDate { get; set; }

    public DateTime? NextRetryProcessAfter { get; set; }

    public string LastConsumeError { get; set; }

    public string? ConcurrencyUpdateToken { get; set; }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CanHandleMessagesExpr(
        double messageProcessingMaximumTimeInSeconds,
        string? forApplicationName)
    {
        Expression<Func<PlatformInboxBusMessage, bool>> initialExpr =
            p => p.ConsumeStatus == ConsumeStatuses.New ||
                 (p.ConsumeStatus == ConsumeStatuses.Failed &&
                  (p.NextRetryProcessAfter == null || p.NextRetryProcessAfter <= DateTime.UtcNow)) ||
                 (p.ConsumeStatus == ConsumeStatuses.Processing &&
                  p.LastConsumeDate <= Clock.UtcNow.AddSeconds(-messageProcessingMaximumTimeInSeconds - MarginRetryMessageProcessingMaximumTimeInSeconds));

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
        double ignoreExpiredFailedMessageInSeconds)
    {
        return p => p.CreatedDate <= Clock.UtcNow.AddSeconds(-ignoreExpiredFailedMessageInSeconds) &&
                    p.ConsumeStatus == ConsumeStatuses.Failed;
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
        return $"{BuildIdGroupedByConsumerPrefix(consumerType, extendedMessageIdPrefix)}{BuildIdSeparator}{trackId ?? Ulid.NewUlid().ToString()}".TakeTop(IdMaxLength);
    }

    public string GetIdPrefix()
    {
        return GetIdPrefix(Id);
    }

    public static string GetIdPrefix(string messageId)
    {
        var buildIdSeparatorIndex = messageId.IndexOf(BuildIdSeparator, StringComparison.Ordinal);

        return messageId.Substring(0, buildIdSeparatorIndex > 0 ? buildIdSeparatorIndex : messageId.Length);
    }

    public static string BuildIdGroupedByConsumerPrefix(Type consumerType, string extendedMessageIdPrefix)
    {
        return extendedMessageIdPrefix.IsNullOrEmpty()
            ? consumerType.GetNameOrGenericTypeName()
            : $"{consumerType.GetNameOrGenericTypeName()}{BuildIdGroupedByConsumerPrefixSeparator}{extendedMessageIdPrefix}";
    }

    public static DateTime CalculateNextRetryProcessAfter(
        int? retriedProcessCount,
        double retryProcessFailedMessageInSecondsUnit = DefaultRetryProcessFailedMessageInSecondsUnit)
    {
        return DateTime.UtcNow.AddSeconds(
            retryProcessFailedMessageInSecondsUnit * retriedProcessCount ?? 0);
    }

    public static PlatformInboxBusMessage Create<TMessage>(
        TMessage message,
        string trackId,
        string produceFrom,
        string routingKey,
        Type consumerType,
        ConsumeStatuses consumeStatus,
        string forApplicationName,
        string extendedMessageIdPrefix) where TMessage : class
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
            RetriedProcessCount = 0,
            ForApplicationName = forApplicationName
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
