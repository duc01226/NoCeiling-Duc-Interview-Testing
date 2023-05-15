namespace Easy.Platform.Domain.Entities;

/// <summary>
/// Ensure concurrent update is not conflicted
/// </summary>
public interface IRowVersionEntity : IEntity
{
    /// <summary>
    /// This is used as a Concurrency Token to track entity version to prevent concurrent update
    /// </summary>
    public Guid? ConcurrencyUpdateToken { get; set; }
}
