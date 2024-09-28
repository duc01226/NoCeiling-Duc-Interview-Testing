using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Persistence.DataMigration;

public class PlatformDataMigrationHistory : IRowVersionEntity
{
    public const string DbInitializedMigrationHistoryName = "DbInitialized";
    public const int ProcessingPingIntervalSeconds = 10;
    public const int MaxAllowedProcessingPingMisses = 3;

    private DateTime? createdDate;

    public PlatformDataMigrationHistory()
    {
        CreatedDate = DateTime.UtcNow;
    }

    public PlatformDataMigrationHistory(string name) : this()
    {
        Name = name;
    }

    public Statuses? Status { get; set; } = Statuses.New;

    /// <summary>
    /// Used to determine that the current processing migration is still in processing and not done yet, ping to know that there is at least one service is working on it
    /// </summary>
    public DateTime? LastProcessingPingTime { get; set; }

    public string? LastProcessError { get; set; }

    public string Name { get; set; }

    public DateTime CreatedDate
    {
        get => createdDate ?? DateTime.UtcNow;
        set => createdDate = value;
    }

    public string? ConcurrencyUpdateToken { get; set; }

    public object GetId()
    {
        return Name;
    }

    public bool CanStartOrRetryProcess()
    {
        return !this.Is(ProcessedOrProcessingExpr());
    }

    public static Expression<Func<PlatformDataMigrationHistory, bool>> ProcessedOrProcessingExpr()
    {
        // Null for old PlatformDataMigrationHistory without status
        return p => p.Status == null ||
                    p.Status == Statuses.Processed ||
                    (p.Status == Statuses.Processing &&
                     p.LastProcessingPingTime != null &&
                     p.LastProcessingPingTime >= Clock.Now.AddSeconds(-ProcessingPingIntervalSeconds * MaxAllowedProcessingPingMisses));
    }

    public enum Statuses
    {
        New,
        Processing,
        Processed,
        Failed
    }
}
