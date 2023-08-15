using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

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
        var clientSettings = MongoClientSettings.FromUrl(MongoUrl.Create(options.Value.ConnectionString))
            .With(
                settings => settings.ClusterConfigurator = cb => cb.Subscribe(
                    new DiagnosticsActivityEventSubscriber(new InstrumentationOptions { CaptureCommandText = true })))
            .With(settings => settings.MinConnectionPoolSize = options.Value.MinConnectionPoolSize)
            .With(settings => settings.MaxConnectionPoolSize = options.Value.MaxConnectionPoolSize);

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
