using Easy.Platform.Infrastructures.Caching;
using Easy.Platform.RedisCache;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Api;

/// <summary>
/// Implement RedisCacheModule and use it to use distributed cache via <see cref="PlatformCacheRepositoryType.Distributed"/>
/// </summary>
public class TextSnippetRedisCacheModule : PlatformRedisCacheModule
{
    public TextSnippetRedisCacheModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    protected override void SetupRedisCacheOptions(RedisCacheOptions options)
    {
        options.Configuration = Configuration["RedisCacheOptions:Connection"];
    }

    protected override PlatformCacheEntryOptions DefaultPlatformCacheEntryOptions(IServiceProvider serviceProvider)
    {
        return new PlatformCacheEntryOptions
        {
            AbsoluteExpirationInSeconds = Configuration.GetSection("Caching:DefaultExpirationInSeconds").Get<int>()
        };
    }
}
