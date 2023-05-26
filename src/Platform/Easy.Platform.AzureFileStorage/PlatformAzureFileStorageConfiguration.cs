using Easy.Platform.Infrastructures.FileStorage;

namespace Easy.Platform.AzureFileStorage;

public sealed class PlatformAzureFileStorageConfiguration : PlatformFileStorageOptions
{
    /// <summary>
    /// Connection string to azure blob. Ex: DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;EndpointSuffix=core.windows.net
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Domain url to blob file storage service endpoint. Ex: https://xxxxx.blob.core.windows.net/
    /// </summary>
    public string BlobEndpoint { get; set; }

    /// <summary>
    /// Default expiration time span for uri when generate share private file url in minutes
    /// </summary>
    public int DefaultSharedPrivateFileUriAccessTimeMinutes { get; set; } = 60;
}
