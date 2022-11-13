namespace Easy.Platform.MongoDB;

public class PlatformMongoOptions
{
    public string ConnectionString { get; set; }
    public string Database { get; set; }
}

public class PlatformMongoOptions<TDbContext> : PlatformMongoOptions
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}
