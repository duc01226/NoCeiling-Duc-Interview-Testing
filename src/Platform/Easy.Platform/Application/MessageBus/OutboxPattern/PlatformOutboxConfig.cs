namespace Easy.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxConfig
{
    /// <summary>
    /// This is used to calculate the next retry process message time.
    /// Ex: NextRetryProcessAfterDate = DateTime.UtcNow.AddSeconds(retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    /// </summary>
    public double RetryProcessFailedMessageInSecondsUnit { get; set; } = PlatformOutboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit;

    /// <summary>
    /// Set StandaloneUowForOutbox = true only when apply platform for old code/project have not open and complete uow. Remove it after finish refactoring
    /// </summary>
    public bool StandaloneUowForOutbox { get; set; }
}
