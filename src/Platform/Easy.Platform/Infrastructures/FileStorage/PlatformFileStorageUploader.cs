using System.Diagnostics.CodeAnalysis;
using System.IO;
using Easy.Platform.Common.Extensions;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.Infrastructures.FileStorage;

public class PlatformFileStorageUploader
{
    private string contentType;
    private PlatformFileStorageUploader() { }

    public string ContentType
    {
        get => contentType.IsNullOrEmpty() ? PlatformFileMimeTypeMapper.Instance.GetMimeType(FileName) : contentType;
        set => contentType = value;
    }

    public string RootDirectory { get; private init; }

    /// <summary>
    /// The following directory after rootDirectory, should define this more detail for good practice
    /// NOTICE: this should not include the fileName, just prefix directory
    /// </summary>
    public string PrefixDirectoryPath { get; private init; }

    public string FileName { get; private init; }

    public string FileDescription { get; set; }

    public Stream Stream { get; private init; }

    public PlatformFileStorageOptions.PublicAccessTypes PublicAccessType { get; set; } = PlatformFileStorageOptions.PublicAccessTypes.None;

    public static PlatformFileStorageUploader Create(
        Stream stream,
        [NotNull] string prefixDirectoryPath,
        [NotNull] string fileName,
        [NotNull] string rootDirectory,
        PlatformFileStorageOptions.PublicAccessTypes publicAccessType,
        string contentType = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(prefixDirectoryPath);
        ArgumentNullException.ThrowIfNull(fileName);

        return new PlatformFileStorageUploader
        {
            Stream = stream,
            RootDirectory = rootDirectory,
            PrefixDirectoryPath = prefixDirectoryPath,
            FileName = fileName,
            ContentType = contentType,
            PublicAccessType = publicAccessType
        };
    }

    public static PlatformFileStorageUploader Create(
        Stream stream,
        [NotNull] string prefixDirectoryPath,
        [NotNull] string fileName,
        string contentType = null)
    {
        return Create(
            stream,
            prefixDirectoryPath,
            fileName,
            rootDirectory: IPlatformFileStorageService.GetDefaultRootDirectoryName(isPrivate: true),
            publicAccessType: IPlatformFileStorageService.GetDefaultPublicAccessType(isPrivate: true),
            contentType);
    }

    public static PlatformFileStorageUploader Create(
        IFormFile formFile,
        [NotNull] string prefixDirectoryPath,
        [NotNull] string rootDirectory,
        PlatformFileStorageOptions.PublicAccessTypes publicAccessType,
        string fileName = null,
        string contentType = null,
        string fileDescription = null)
    {
        return new PlatformFileStorageUploader
        {
            Stream = formFile.OpenReadStream(),
            RootDirectory = rootDirectory,
            PrefixDirectoryPath = prefixDirectoryPath,
            FileName = fileName ?? formFile.FileName,
            ContentType = contentType,
            FileDescription = fileDescription,
            PublicAccessType = publicAccessType
        };
    }

    public static PlatformFileStorageUploader Create(
        IFormFile formFile,
        [NotNull] string prefixDirectoryPath,
        string fileName = null,
        string contentType = null,
        string fileDescription = null)
    {
        return Create(
            formFile,
            prefixDirectoryPath,
            rootDirectory: IPlatformFileStorageService.GetDefaultRootDirectoryName(isPrivate: true),
            publicAccessType: IPlatformFileStorageService.GetDefaultPublicAccessType(isPrivate: true),
            fileName,
            contentType,
            fileDescription);
    }

    public static PlatformFileStorageUploader Create(
        IPlatformFile platformFileInfo,
        [NotNull] string prefixDirectoryPath,
        [NotNull] string rootDirectory,
        PlatformFileStorageOptions.PublicAccessTypes publicAccessType,
        string fileName = null,
        string contentType = null,
        string fileDescription = null)
    {
        return new PlatformFileStorageUploader
        {
            Stream = platformFileInfo.OpenReadStream(),
            RootDirectory = rootDirectory,
            PrefixDirectoryPath = prefixDirectoryPath,
            FileName = fileName ?? platformFileInfo.GetFileName(),
            ContentType = contentType,
            FileDescription = fileDescription,
            PublicAccessType = publicAccessType
        };
    }

    public static PlatformFileStorageUploader Create(
        IPlatformFile platformFileInfo,
        [NotNull] string prefixDirectoryPath,
        string fileName = null,
        string contentType = null,
        string fileDescription = null)
    {
        return Create(
            platformFileInfo,
            prefixDirectoryPath,
            rootDirectory: IPlatformFileStorageService.GetDefaultRootDirectoryName(isPrivate: true),
            publicAccessType: IPlatformFileStorageService.GetDefaultPublicAccessType(isPrivate: true),
            fileName,
            contentType,
            fileDescription);
    }

    /// <summary>
    /// Create a path to storage content on cloud, this method will join each param with a <c>'/'</c>
    /// <para>NOTICE: best practice with Azure: {container}/{app}/{service}/{guid-com-id}/...</para>
    /// <para>Ex: private/talents/candidates/my-file.pdf</para>
    /// </summary>
    public static string CombinePath(params string[] elements)
    {
        return string.Join('/', elements.Where(el => el.IsNotNullOrEmpty()));
    }
}
