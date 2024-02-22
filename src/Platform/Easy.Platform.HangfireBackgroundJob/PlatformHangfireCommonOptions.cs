namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireCommonOptions
{
    public static readonly TimeSpan DefaultJobExpirationCheckInterval = 30.Minutes();
    public static readonly TimeSpan DefaultCountersAggregateInterval = 5.Minutes();
    public static readonly TimeSpan DefaultQueuePollInterval = 15.Seconds();

    /// <summary>
    /// Define how long a succeeded job should stayed before being deleted
    /// </summary>
    public int JobSucceededExpirationTimeoutSeconds { get; set; } = 180;
}
