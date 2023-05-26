namespace Easy.Platform.AzureFileStorage;

/// <summary>
/// Azure Storage is structured as the first folder is container
/// Following path will structure as blobName
/// </summary>
public sealed class PlatformAzureBlobPathInfo
{
    public PlatformAzureBlobPathInfo(string containerName, string blobName)
    {
        ContainerName = containerName;
        BlobName = blobName;
    }

    /// <summary>
    /// Root directory on azure storage account
    /// </summary>
    public string ContainerName { get; set; }

    /// <summary>
    /// The following directory partition including filename
    /// </summary>
    public string BlobName { get; set; }

    /// <summary>
    /// Split absolute path of Azure storage to container and blobName
    /// </summary>
    public static PlatformAzureBlobPathInfo Create(string fullFilePath)
    {
        var blobPathSplit = fullFilePath.TrimStart('/').Split('/');

        var containerName = blobPathSplit[0];
        var relativePath = string.Join('/', blobPathSplit.Skip(1));

        return Create(containerName, relativePath);
    }

    public static PlatformAzureBlobPathInfo Create(string rootDirectory, string filePath)
    {
        return new PlatformAzureBlobPathInfo(rootDirectory, filePath);
    }
}
