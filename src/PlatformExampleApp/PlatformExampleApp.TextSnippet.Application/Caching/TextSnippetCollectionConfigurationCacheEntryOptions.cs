using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Application.Caching;

public class TextSnippetCollectionConfigurationCacheEntryOptions : PlatformConfigurationCacheEntryOptions
{
    public TextSnippetCollectionConfigurationCacheEntryOptions(IConfiguration configuration) : base(configuration)
    {
    }

    public override double? AbsoluteExpirationInSeconds =>
        Configuration.GetSection("Caching:TextSnippetCollectionExpirationInSeconds").Get<int>();

    public override double? UnusedExpirationInSeconds { get; set; }
}
