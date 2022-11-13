using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Easy.Platform.MongoDB;

public interface IPlatformMongoClient
{
    public MongoClient MongoClient { get; }
}

public interface IPlatformMongoClient<TDbContext> : IPlatformMongoClient
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
}

public class PlatformMongoClient : IPlatformMongoClient
{
    public PlatformMongoClient(IOptions<PlatformMongoOptions> options)
    {
        MongoClient = new MongoClient(options.Value.ConnectionString);
    }

    public MongoClient MongoClient { get; set; }
}

public class PlatformMongoClient<TDbContext>
    : PlatformMongoClient, IPlatformMongoClient<TDbContext> where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformMongoClient(IOptions<PlatformMongoOptions<TDbContext>> options) : base(options)
    {
    }
}
