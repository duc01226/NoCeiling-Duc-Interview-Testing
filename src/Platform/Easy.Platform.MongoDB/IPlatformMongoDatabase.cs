using Easy.Platform.Application.Persistence;
using MongoDB.Driver;

namespace Easy.Platform.MongoDB;

public interface IPlatformMongoDatabase<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>, IPlatformDbContext<TDbContext>
{
    public IMongoDatabase Value { get; }
}

public class PlatformMongoDatabase<TDbContext> : IPlatformMongoDatabase<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>, IPlatformDbContext<TDbContext>
{
    public PlatformMongoDatabase(IMongoDatabase value)
    {
        Value = value;
    }

    public IMongoDatabase Value { get; }
}
