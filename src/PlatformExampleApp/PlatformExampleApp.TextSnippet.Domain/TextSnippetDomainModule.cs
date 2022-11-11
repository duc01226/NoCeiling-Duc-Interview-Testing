using Easy.Platform.Domain;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Domain;

public class TextSnippetDomainModule : PlatformDomainModule
{
    public TextSnippetDomainModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }
}
