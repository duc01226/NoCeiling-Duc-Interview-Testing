namespace Easy.Platform.Application.MessageBus.InboxPattern;

public class PlatformInboxConfig
{
    /// <summary>
    /// This is used to calculate the next retry process message time.
    /// Ex: NextRetryProcessAfter = DateTime.UtcNow.AddSeconds(retryProcessFailedMessageInSecondsUnit * Math.Pow(2, retriedProcessCount ?? 0));
    /// </summary>
    public double RetryProcessFailedMessageInSecondsUnit { get; set; } =
        PlatformInboxBusMessage.DefaultRetryProcessFailedMessageInSecondsUnit;
}
