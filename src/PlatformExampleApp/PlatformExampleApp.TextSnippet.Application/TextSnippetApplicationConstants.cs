namespace PlatformExampleApp.TextSnippet.Application;

public static class TextSnippetApplicationConstants
{
    public const string ApplicationName = "PlatformExample.AppTextSnippet.Api";
    public static int DefaultBackgroundJobWorkerCount => Math.Min(Environment.ProcessorCount * 2, 10);

    public static class CacheKeyCollectionNames
    {
        public const string TextSnippet = "TextSnippet";
    }
}
