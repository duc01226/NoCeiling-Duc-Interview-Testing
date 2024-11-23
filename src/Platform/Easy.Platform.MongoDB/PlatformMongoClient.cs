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
        var clientSettings = MongoClientSettings.FromUrl(
            new MongoUrlBuilder(options.Value.ConnectionString)
                .With(p => p.MinConnectionPoolSize = options.Value.MinConnectionPoolSize)
                .With(p => p.MaxConnectionPoolSize = options.Value.MaxConnectionPoolSize)
                .ToMongoUrl());

        MongoClient = new MongoClient(clientSettings);
    }

    public MongoClient MongoClient { get; init; }
}

public class PlatformMongoClient<TDbContext>
    : PlatformMongoClient, IPlatformMongoClient<TDbContext> where TDbContext : PlatformMongoDbContext<TDbContext>
{
    public PlatformMongoClient(IOptions<PlatformMongoOptions<TDbContext>> options) : base(options)
    {
    }
}
