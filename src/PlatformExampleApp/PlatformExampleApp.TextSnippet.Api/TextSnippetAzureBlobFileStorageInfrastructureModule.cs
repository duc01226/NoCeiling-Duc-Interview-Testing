using Easy.Platform.AzureFileStorage;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Api;

public class TextSnippetAzureBlobFileStorageInfrastructureModule : PlatformAzureFileStorageModule
{
    public TextSnippetAzureBlobFileStorageInfrastructureModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override void FileStorageConfigurationConfigure(
        IServiceProvider serviceProvider,
        PlatformAzureFileStorageConfiguration configs)
    {
        configs.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;EndpointSuffix=core.windows.net";
        configs.BlobEndpoint = "https://xxxxx.blob.core.windows.net/";
    }
}
