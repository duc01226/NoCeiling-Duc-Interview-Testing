namespace PlatformExampleApp.Test.DataModels;

public class TextSnippetData
{
    public TextSnippetData()
    {
    }

    public TextSnippetData(string snippetText, string fulltext)
    {
        SnippetText = snippetText;
        FullText = fulltext;
    }

    public string SnippetText { get; set; } = "";
    public string FullText { get; set; } = "";
}
