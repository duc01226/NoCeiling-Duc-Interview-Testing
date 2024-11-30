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
                .WithIf(options.Value.MinConnectionPoolSize.HasValue, p => p.MinConnectionPoolSize = options.Value.MinConnectionPoolSize!.Value)
                .WithIf(options.Value.MaxConnectionPoolSize.HasValue, p => p.MaxConnectionPoolSize = options.Value.MaxConnectionPoolSize!.Value)
                .WithIf(options.Value.MaxConnectionIdleTimeSeconds.HasValue, p => p.MaxConnectionIdleTime = options.Value.MaxConnectionIdleTimeSeconds!.Value.Seconds())
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
