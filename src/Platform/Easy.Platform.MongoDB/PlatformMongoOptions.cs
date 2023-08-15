namespace Easy.Platform.MongoDB;

public class PlatformMongoOptions
{
    public string ConnectionString { get; set; }
    public string Database { get; set; }
    public int MinConnectionPoolSize { get; set; }
    public int MaxConnectionPoolSize { get; set; } = 100;
}

public class PlatformMongoOptions<TDbContext> : PlatformMongoOptions
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}
