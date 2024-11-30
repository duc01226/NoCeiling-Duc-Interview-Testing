using Easy.Platform.Persistence;

namespace Easy.Platform.MongoDB;

public class PlatformMongoOptions
{
    public string ConnectionString { get; set; }
    public string Database { get; set; }
    public int? MinConnectionPoolSize { get; set; }
    public int? MaxConnectionPoolSize { get; set; }

    /// <summary>
    /// To configure a DbContext to release its connection shortly after being idle
    ///  => prevent max connection pool error, no connection if a db-context is idling (example run paging for a long time but has opened a db context outside and wait)
    /// </summary>
    public int? MaxConnectionIdleTimeSeconds { get; set; }
}

public class PlatformMongoOptions<TDbContext> : PlatformMongoOptions
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}
