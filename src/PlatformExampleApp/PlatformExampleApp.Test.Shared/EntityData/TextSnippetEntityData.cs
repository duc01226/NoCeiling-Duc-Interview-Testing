using Easy.Platform.AutomationTest.EntityData;

namespace PlatformExampleApp.Test.Shared.EntityData;

public class TextSnippetEntityData : AutomationTestEntityData
{
    public TextSnippetEntityData()
    {
    }

    public TextSnippetEntityData(string? snippetText, string? fulltext)
    {
        SnippetText = snippetText ?? "";
        FullText = fulltext ?? "";
    }

    public string SnippetText { get; set; } = "";
    public string FullText { get; set; } = "";

    public static class Errors
    {
        public const string DuplicatedSnippetTextErrorMsg = "SnippetText must be unique";
    }
}
