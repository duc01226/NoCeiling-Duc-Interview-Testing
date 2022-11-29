namespace Easy.Platform.Infrastructures.FileStorage;

public interface IPlatformFileStorageFileItem
{
    public string FullFilePath { get; }
    public DateTimeOffset? LastModified { get; }
    public string ContentType { get; }
    public string Etag { get; }
    public long Size { get; }
    public string Description { get; }
    public string AbsoluteUri { get; }
}
