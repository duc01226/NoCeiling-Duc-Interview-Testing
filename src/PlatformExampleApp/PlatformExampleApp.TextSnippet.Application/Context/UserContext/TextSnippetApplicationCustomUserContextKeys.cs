using Easy.Platform.Application.Context.UserContext;

namespace PlatformExampleApp.TextSnippet.Application.Context.UserContext;

/// <summary>
/// An example if your application have custom user context data which you want to added into user context
/// In this example imaging in jwt claim types you have "Organizations"
/// </summary>
public static class TextSnippetApplicationCustomUserContextKeys
{
    public const string Organizations = "TextSnippet-Organizations";

    public static List<string> GetOrganization(this IPlatformApplicationUserContext context)
    {
        return context.GetValue<List<string>>(Organizations);
    }
}
