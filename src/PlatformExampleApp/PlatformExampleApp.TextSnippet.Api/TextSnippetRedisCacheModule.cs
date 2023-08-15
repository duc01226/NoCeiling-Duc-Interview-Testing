using Easy.Platform.Infrastructures.Caching;
using Easy.Platform.RedisCache;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Api;

/// <summary>
/// Implement RedisCacheModule and use it to use distributed cache via <see cref="PlatformCacheRepositoryType.Distributed" />
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

    // Demo config PlatformCacheSettings
    protected override void ConfigCacheSettings(IServiceProvider sp, PlatformCacheSettings cacheSettings)
    {
        cacheSettings.SlowWarning.IsEnabled = true;
        cacheSettings.DefaultCacheEntryOptions.AbsoluteExpirationInSeconds = Configuration.GetSection("Caching:DefaultExpirationInSeconds").Get<int>();
    }
}
