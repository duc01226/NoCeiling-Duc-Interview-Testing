namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxConfig
{
    private double? messageProcessingMaxSecondsTimeout;

    /// <summary>
    /// This is used to calculate the next retry process message time.
    /// Ex: NextRetryProcessAfterDate = DateTime.UtcNow.AddSeconds(retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    /// </summary>
    public double RetryProcessFailedMessageInSecondsUnit { get; set; } = PlatformOutboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit;

    /// <summary>
    /// Set StandaloneScopeForOutbox = true only when apply platform for old code/project have not open and complete uow. Remove it after finish refactoring
    /// </summary>
    public bool StandaloneScopeForOutbox { get; set; }

    /// <summary>
    /// To config how long a message can live in the database in seconds. Default is one week (14 day);
    /// </summary>
    public double DeleteProcessedMessageInSeconds { get; set; } = TimeSpan.FromDays(14).TotalSeconds;

    /// <summary>
    /// To config max store processed message count. Will delete old messages of maximum messages happened
    /// </summary>
    public int MaxStoreProcessedMessageCount { get; set; } = 100;

    /// <summary>
    /// To config how long a message can live in the database as Failed in seconds. Default is two week (28 days); After that the message will be automatically ignored by change status to Ignored
    /// </summary>
    public double IgnoreExpiredFailedMessageInSeconds { get; set; } = TimeSpan.FromDays(28).TotalSeconds;

    /// <summary>
    /// To config how long a message can live in the database as Ignored in seconds. Default is one month (365 days); After that the message will be automatically deleted
    /// </summary>
    public double DeleteExpiredIgnoredMessageInSeconds { get; set; } = TimeSpan.FromDays(365).TotalSeconds;

    /// <summary>
    /// Default number messages is processed to be Deleted/Ignored in batch. Default is 100;
    /// </summary>
    public int NumberOfDeleteMessagesBatch { get; set; } = 10;

    public double MessageCleanerTriggerIntervalInMinutes { get; set; } = 1;

    public int ProcessClearMessageRetryCount { get; set; } = 5;

    public int NumberOfProcessSendOutboxParallelMessages { get; set; } = Environment.ProcessorCount * 4;

    public int NumberOfProcessSendOutboxMessagesSubQueuePrefetch { get; set; } = 5;

    public int GetCanHandleMessageGroupedByTypeIdPrefixesPageSize { get; set; } = 10000;

    public int ProcessSendMessageRetryCount { get; set; } = 10;

    /// <summary>
    /// To config how long a message can live in the database as Processing status in seconds. Default is 300 seconds;
    /// This to handle that if message for some reason has been set as Processing but failed to process and has not been set
    /// back to failed.
    /// </summary>
    public int MessageProcessingMaxSeconds { get; set; } = 300;

    public double MessageProcessingMaxSecondsTimeoutRatio { get; set; } = 0.9;

    public double MessageProcessingMaxSecondsTimeout
    {
        get
        {
            messageProcessingMaxSecondsTimeout ??= CalcMessageProcessingMaxSecondsTimeout();
            return messageProcessingMaxSecondsTimeout!.Value;
        }
        set => messageProcessingMaxSecondsTimeout = value;
    }

    public bool LogIntervalProcessInformation { get; set; }

    public int CheckToProcessTriggerIntervalTimeSeconds { get; set; } = 15;

    public int MinimumRetrySendOutboxMessageTimesToWarning { get; set; } = 2;

    public double? CalcMessageProcessingMaxSecondsTimeout()
    {
        return MessageProcessingMaxSeconds * MessageProcessingMaxSecondsTimeoutRatio;
    }
}
