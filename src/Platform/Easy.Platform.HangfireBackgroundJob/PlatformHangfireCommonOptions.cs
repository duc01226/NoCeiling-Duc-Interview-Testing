namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireCommonOptions
{
    public static TimeSpan DefaultJobExpirationCheckInterval => 1.Minutes();

    /// <summary>
    /// Define how long a succeeded job should stayed before being deleted
    /// </summary>
    public int JobSucceededExpirationTimeoutSeconds { get; set; } = 180;
}
