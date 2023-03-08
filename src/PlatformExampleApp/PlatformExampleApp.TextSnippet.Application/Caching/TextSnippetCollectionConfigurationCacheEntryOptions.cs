using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Application.Caching;

// Extend from this class to define CacheEntryOptions which read from IConfiguration. This will be register and replace the default PlatformCacheEntryOptions
// This is optional and not required
public class TextSnippetCollectionConfigurationCacheEntryOptions : PlatformConfigurationCacheEntryOptions
{
    public TextSnippetCollectionConfigurationCacheEntryOptions(IConfiguration configuration) : base(configuration)
    {
    }

    public override double? AbsoluteExpirationInSeconds => Configuration.GetSection("Caching:TextSnippetCollectionExpirationInSeconds").Get<int>();

    public override double? UnusedExpirationInSeconds { get; set; }
}
