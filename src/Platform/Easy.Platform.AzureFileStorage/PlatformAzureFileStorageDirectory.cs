using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Easy.Platform.Infrastructures.FileStorage;

namespace Easy.Platform.AzureFileStorage;

public class PlatformAzureFileStorageDirectory : IPlatformFileStorageDirectory
{
    private static readonly HashSet<string> ReservedFileNames = new()
    {
        ".",
        "..",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "PRN",
        "AUX",
        "NUL",
        "CON",
        "CLOCK$"
    };

    public PlatformAzureFileStorageDirectory(BlobContainerClient blobContainer, string directoryRelativePath)
    {
        Uri = blobContainer.Uri;
        BlobContainer = blobContainer;
        Prefix = directoryRelativePath;
        ContainerName = blobContainer.Name;
    }

    public BlobContainerClient BlobContainer { get; }

    public string ContainerName { get; }
    public Uri Uri { get; set; }
    public string Prefix { get; set; }

    public IPlatformFileStorageDirectory GetDirectoryReference(string directoryRelativePath)
    {
        return new PlatformAzureFileStorageDirectory(
            BlobContainer,
            $"{Prefix}/{directoryRelativePath}");
    }

    public IEnumerable<IPlatformFileStorageFileItem> GetFileItems()
    {
        var result = BlobContainer.GetBlobs(BlobTraits.Metadata, BlobStates.None, Prefix)
            .Where(p => !ReservedFileNames.Any(_ => p.Name.EndsWith(_)))
            .Select(p => PlatformAzureFileStorageFileItem.Create(p, BlobContainer));

        return result;
    }
}
