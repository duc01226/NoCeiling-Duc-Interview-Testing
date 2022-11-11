namespace Easy.Platform.Infrastructures.BackgroundJob;

public interface IPlatformBackgroundJobProcessingService
{
    /// <summary>
    /// Check is the job processing service started
    /// </summary>
    /// <returns></returns>
    bool Started();

    /// <summary>
    /// Start the job processing service to process background job
    /// </summary>
    Task Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the job processing service.
    /// </summary>
    Task Stop(CancellationToken cancellationToken = default);
}
