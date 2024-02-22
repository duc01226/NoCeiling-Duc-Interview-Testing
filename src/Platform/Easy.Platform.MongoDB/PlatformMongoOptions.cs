namespace Easy.Platform.MongoDB;

public class PlatformMongoOptions
{
    public string ConnectionString { get; set; }
    public string Database { get; set; }
    public int MinConnectionPoolSize { get; set; } = 1;
    public int MaxConnectionPoolSize { get; set; } = 50;
}

public class PlatformMongoOptions<TDbContext> : PlatformMongoOptions
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}
