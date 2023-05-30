namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxConfig
{
    /// <summary>
    /// This is used to calculate the next retry process message time.
    /// Ex: NextRetryProcessAfter = DateTime.UtcNow.AddSeconds(retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    /// </summary>
    public double RetryProcessFailedMessageInSecondsUnit { get; set; } = PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit;

    /// <summary>
    /// To config how long a processed message can live in the database in seconds. Default is one week (2 days);
    /// </summary>
    public double DeleteProcessedMessageInSeconds { get; set; } = TimeSpan.FromDays(2).TotalSeconds;

    /// <summary>
    /// To config how long a message can live in the database in seconds. Default is two week (14 days);
    /// </summary>
    public double DeleteExpiredFailedMessageInSeconds { get; set; } = TimeSpan.FromDays(14).TotalSeconds;

    /// <summary>
    /// To config maximum number messages is deleted in every process. Default is 10
    /// </summary>
    public int NumberOfDeleteMessagesBatch { get; set; } = 10;

    public double MessageCleanerTriggerIntervalInMinutes { get; set; } = 1;

    public int ProcessClearMessageRetryCount { get; set; } = 5;

    /// <summary>
    /// Config the time to true to log consumer process time
    /// </summary>
    public bool IsLogConsumerProcessTime { get; set; } = true;

    /// <summary>
    /// Config the time in milliseconds to log warning if the process consumer time is over LogConsumerProcessWarningTimeMilliseconds.
    /// </summary>
    public long LogErrorSlowProcessWarningTimeMilliseconds { get; set; } = 5000;

    // Default should be 1 per time like multi thread/instance handle once at a time. Also we have not found out way to process parallel messages
    // because need to detect message order dependency for a same person or entity target
    // Support deploy multiple instance horizontal scale
    public int NumberOfProcessConsumeInboxMessagesBatch { get; set; } = 1;

    public int ProcessConsumeMessageRetryCount { get; set; } = 10;

    /// <summary>
    /// To config how long a message can live in the database as Processing status in seconds. Default is 3600 * 24 seconds;
    /// This to handle that if message for some reason has been set as Processing but failed to process and has not been set
    /// back to failed.
    /// </summary>
    public double MessageProcessingMaximumTimeInSeconds { get; set; } = 3600 * 24;
}
