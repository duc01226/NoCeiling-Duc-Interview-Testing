namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxConfig
{
    private double? messageProcessingMaxSecondsTimeout;

    /// <summary>
    /// This is used to calculate the next retry process message time.
    /// Ex: NextRetryProcessAfter = DateTime.UtcNow.AddSeconds(retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    /// </summary>
    public double RetryProcessFailedMessageInSecondsUnit { get; set; } = PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit;

    /// <summary>
    /// To config how long a processed message can live in the database in seconds. Default is one week (14 days);
    /// </summary>
    public double DeleteProcessedMessageInSeconds { get; set; } = TimeSpan.FromDays(14).TotalSeconds;

    /// <summary>
    /// To config max store processed message count. Will delete old messages of maximum messages happened
    /// </summary>
    public int MaxStoreProcessedMessageCount { get; set; } = 1000;

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
    public int NumberOfDeleteMessagesBatch { get; set; } = 100;

    public double MessageCleanerTriggerIntervalInMinutes { get; set; } = 1;

    public int ProcessClearMessageRetryCount { get; set; } = 5;

    public int NumberOfProcessConsumeInboxMessagesBatch { get; set; } = 5;

    public int ProcessConsumeMessageRetryCount { get; set; } = 10;

    /// <summary>
    /// To config how long a message can live in the database as Processing status in seconds. Default is 3600 seconds;
    /// This to handle that if message for some reason has been set as Processing but failed to process and has not been set
    /// back to failed.
    /// </summary>
    public double MessageProcessingMaxSeconds { get; set; } = 3600;

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

    public int MinimumRetryConsumeInboxMessageTimesToWarning { get; set; } = 3;

    public bool LogIntervalProcessInformation { get; set; }

    public int CheckToProcessTriggerIntervalTimeSeconds { get; set; } = 15;

    public double? CalcMessageProcessingMaxSecondsTimeout()
    {
        return MessageProcessingMaxSeconds * MessageProcessingMaxSecondsTimeoutRatio;
    }
}
