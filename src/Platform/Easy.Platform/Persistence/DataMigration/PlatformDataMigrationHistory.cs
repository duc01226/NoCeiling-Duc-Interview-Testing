namespace Easy.Platform.Persistence.DataMigration;

public class PlatformDataMigrationHistory
{
    private DateTime? createdDate;

    public PlatformDataMigrationHistory()
    {
    }

    public PlatformDataMigrationHistory(string name)
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
