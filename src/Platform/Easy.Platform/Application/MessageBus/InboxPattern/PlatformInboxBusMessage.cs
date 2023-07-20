using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Infrastructures.MessageBus;

namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxBusMessage : RootEntity<PlatformInboxBusMessage, string>, IRowVersionEntity
{
    public const int IdMaxLength = 200;
    public const int MessageTypeFullNameMaxLength = 1000;
    public const int RoutingKeyMaxLength = 500;
    public const double DefaultRetryProcessFailedMessageInSecondsUnit = 60;
    public const string BuildIdSeparator = "---";

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

    public DateTime CreatedDate { get; set; }

    public DateTime LastConsumeDate { get; set; }

    public DateTime? NextRetryProcessAfter { get; set; }

    public string LastConsumeError { get; set; }

    public Guid? ConcurrencyUpdateToken { get; set; }

    public static string SerializeOneLevelExceptionForLastConsumeError(Exception exception)
    {
        return PlatformJsonSerializer.Serialize(
            new
            {
                exception.Message,
                InnerException = exception.InnerException?.Pipe(ex => SerializeOneLevelExceptionForLastConsumeError(ex)),
                exception.StackTrace
            });
    }

    public static Expression<Func<PlatformInboxBusMessage, bool>> CanHandleMessagesExpr(
        double messageProcessingMaximumTimeInSeconds)
    {
        return p => p.ConsumeStatus == ConsumeStatuses.New ||
                    (p.ConsumeStatus == ConsumeStatuses.Failed &&
                     (p.NextRetryProcessAfter == null || p.NextRetryProcessAfter <= DateTime.UtcNow)) ||
                    (p.ConsumeStatus == ConsumeStatuses.Processing &&
                     p.LastConsumeDate <= Clock.UtcNow.AddSeconds(-messageProcessingMaximumTimeInSeconds));
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

    public static string BuildId(string trackId, Type consumerType)
    {
        return $"{trackId ?? Guid.NewGuid().ToString()}{BuildIdSeparator}{consumerType.Name}".TakeTop(IdMaxLength);
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
        string lastConsumeError = null) where TMessage : class
    {
        var nowDate = Clock.UtcNow;

        var result = new PlatformInboxBusMessage
        {
            Id = BuildId(trackId, consumerType),
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

    public static string GetConsumerByValue<TMessage>(IPlatformMessageBusConsumer<TMessage> consumer)
        where TMessage : class, new()
    {
        return GetConsumerByValue(consumer.GetType());
    }

    public static string GetConsumerByValue(Type consumerType)
    {
        return consumerType.FullName;
    }

    public string GetTrackId()
    {
        return Id.Split(BuildIdSeparator).FirstOrDefault();
    }

    public enum ConsumeStatuses
    {
        New,
        Processing,
        Processed,
        Failed
    }
}
