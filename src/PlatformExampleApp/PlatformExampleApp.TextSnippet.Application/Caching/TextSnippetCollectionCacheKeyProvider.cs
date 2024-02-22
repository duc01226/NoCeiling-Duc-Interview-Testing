using Easy.Platform.Application;
using Easy.Platform.Application.Caching;

namespace PlatformExampleApp.TextSnippet.Application.Caching;

public class TextSnippetCollectionCacheKeyProvider : PlatformApplicationCollectionCacheKeyProvider<TextSnippetCollectionCacheKeyProvider>
{
    public TextSnippetCollectionCacheKeyProvider(IPlatformApplicationSettingContext applicationSettingContext) :
        base(applicationSettingContext)
    {
    }

    public override string Collection => TextSnippetApplicationConstants.CacheKeyCollectionNames.TextSnippet;
}
