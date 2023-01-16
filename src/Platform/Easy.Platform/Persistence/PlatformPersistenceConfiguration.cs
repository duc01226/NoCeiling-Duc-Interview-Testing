namespace Easy.Platform.Persistence;

public class PlatformPersistenceConfiguration<TDbContext>
{
    public bool ForCrossDbMigrationOnly { get; set; }
}
