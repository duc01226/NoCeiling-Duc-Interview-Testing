namespace Easy.Platform.Persistence.DataMigration;

public class PlatformDataMigrationHistory
{
    public const string DbInitializedMigrationHistoryName = "DbInitialized";

    private DateTime? createdDate;

    public PlatformDataMigrationHistory()
    {
        CreatedDate = DateTime.UtcNow;
    }

    public PlatformDataMigrationHistory(string name) : this()
    {
        Name = name;
    }

    public string Name { get; set; }

    public DateTime CreatedDate
    {
        get => createdDate ?? DateTime.UtcNow;
        set => createdDate = value;
    }
}
