using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Infrastructures.FileStorage;

public interface IPlatformFileStorageDirectory
{
    public string ContainerName { get; }
    public Uri Uri { get; set; }
    public string Prefix { get; set; }

    public IPlatformFileStorageDirectory GetDirectoryReference(string directoryRelativePath);

    public IEnumerable<IPlatformFileStorageFileItem> GetFileItems();

    public List<IPlatformFileStorageDirectory> GetDirectChildDirectories()
    {
        var result = GetFileItems()
            .AsEnumerable()
            .Select(blobItem => blobItem.FullFilePath.TrimStart('/').Substring(startIndex: Prefix.Length).TrimStart('/').TakeUntilNextChar('/'))
            .Distinct()
            .SelectList(directChildDirectoryName => GetDirectoryReference(directChildDirectoryName));

        return result;
    }
}
