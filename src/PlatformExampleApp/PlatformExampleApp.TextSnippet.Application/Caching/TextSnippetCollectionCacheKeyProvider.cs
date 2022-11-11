using Easy.Platform.Application.Caching;
using Easy.Platform.Application.Context;

namespace PlatformExampleApp.TextSnippet.Application.Caching;

public class TextSnippetCollectionCacheKeyProvider :
    PlatformApplicationCollectionCacheKeyProvider<TextSnippetCollectionCacheKeyProvider>
{
    public TextSnippetCollectionCacheKeyProvider(IPlatformApplicationSettingContext applicationSettingContext) :
        base(applicationSettingContext)
    {
    }

    public override string Collection => TextSnippetApplicationConstants.CacheKeyCollectionNames.TextSnippet;
}
