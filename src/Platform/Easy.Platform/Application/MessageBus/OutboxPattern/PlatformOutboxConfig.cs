namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxConfig
{
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
    /// To config how long a message can live in the database in seconds. Default is one week (2 day);
    /// </summary>
    public double DeleteProcessedMessageInSeconds { get; set; } = TimeSpan.FromDays(2).TotalSeconds;

    /// <summary>
    /// To config max store processed message count. Will delete old messages of maximum messages happened
    /// </summary>
    public int MaxStoreProcessedMessageCount { get; set; } = 100;

    /// <summary>
    /// To config how long a message can live in the database in seconds. Default is two week (14 days);
    /// </summary>
    public double DeleteExpiredFailedMessageInSeconds { get; set; } = TimeSpan.FromDays(14).TotalSeconds;

    /// <summary>
    /// Default number messages is deleted in every process. Default is 100;
    /// </summary>
    public int NumberOfDeleteMessagesBatch { get; set; } = 100;

    public double MessageCleanerTriggerIntervalInMinutes { get; set; } = 1;

    public int ProcessClearMessageRetryCount { get; set; } = 5;

    public int NumberOfProcessSendOutboxMessagesBatch { get; set; } = 100;

    public int ProcessSendMessageRetryCount { get; set; } = 10;

    /// <summary>
    /// To config how long a message can live in the database as Processing status in seconds. Default is 300 seconds;
    /// This to handle that if message for some reason has been set as Processing but failed to process and has not been set
    /// back to failed.
    /// </summary>
    public int MessageProcessingMaxSeconds { get; set; } = 1800;
}
