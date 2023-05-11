using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Infrastructures.FileStorage;

public interface IPlatformFileStorageDirectory
{
    public string RootDirectoryName { get; }
    public Uri DirectoryAbsoluteUri { get; set; }

    /// <summary>
    /// Present the virtual folder path prefix, not including RootDirectory. <br />
    /// Example: a file item path: root/folder1/folder2/fileName.extension. The folder2 will be: <br />
    /// {RootDirectory: root, Prefix: folder1/folder2}
    /// </summary>
    public string DirectoryRelativePathPrefix { get; set; }

    public IPlatformFileStorageDirectory GetRelativeChildDirectory(string directoryRelativePath);

    public IEnumerable<IPlatformFileStorageFileItem> GetFileItems();

    public List<IPlatformFileStorageDirectory> GetDirectChildDirectories()
    {
        var fileItems = GetFileItems().ToList();

        var result = fileItems
            .Select(
                blobItem => blobItem.FullFilePath
                    .TrimStart('/')
                    .Substring(blobItem.RootDirectory.Length)
                    .TrimStart('/')
                    .Substring(startIndex: DirectoryRelativePathPrefix.Length)
                    .TrimStart('/')
                    .TakeUntilNextChar('/'))
            .Distinct()
            .SelectList(GetRelativeChildDirectory);

        return result;
    }
}
