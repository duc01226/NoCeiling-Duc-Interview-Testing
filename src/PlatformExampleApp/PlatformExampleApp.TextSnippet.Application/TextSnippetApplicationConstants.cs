namespace PlatformExampleApp.TextSnippet.Application;

public static class TextSnippetApplicationConstants
{
    public const string ApplicationName = "PlatformExample.AppTextSnippet.Api";
    public static int DefaultBackgroundJobWorkerCount => Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent;

    public static class CacheKeyCollectionNames
    {
        public const string TextSnippet = "TextSnippet";
    }
}
