using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.Infrastructures.FileStorage;

public class PlatformFileStorageUploader
{
    private PlatformFileStorageUploader() { }

    public string ContentType { get; private init; }

    public string RootDirectory { get; private init; }

    /// <summary>
    /// The following directory after rootDirectory, should define this more detail for good practice
    /// NOTICE: this should not include the fileName, just prefix directory
    /// </summary>
    public string PrefixDirectoryPath { get; private init; }

    public string FileName { get; private init; }

    public string FileDescription { get; set; }

    public Stream Stream { get; private init; }

    /// <summary>
    /// Get content of UploadFile. If ContentType is null we will check from file extension
    /// </summary>
    /// <returns></returns>
    public string GetContentType()
    {
        if (!string.IsNullOrEmpty(ContentType)) return ContentType;

        return PlatformFileMimeTypeMapper.Instance.GetMimeType(FileName);
    }

    /// <summary>
    /// Init new upload file
    /// </summary>
    /// <param name="stream">stream content</param>
    /// <param name="prefixDirectoryPath">folder path where to store content on cloud</param>
    /// <param name="fileName">file name on cloud</param>
    /// <param name="isPrivate">Decide this content is public or private</param>
    /// <param name="contentType"></param>
    public static PlatformFileStorageUploader Create(
        Stream stream,
        [NotNull] string prefixDirectoryPath,
        [NotNull] string fileName,
        bool isPrivate = true,
        string contentType = null)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));
        ArgumentNullException.ThrowIfNull(prefixDirectoryPath, nameof(prefixDirectoryPath));
        ArgumentNullException.ThrowIfNull(fileName, nameof(fileName));

        return new PlatformFileStorageUploader
        {
            Stream = stream,
            RootDirectory = IPlatformFileStorageService.GetDefaultRootDirectoryName(isPrivate),
            PrefixDirectoryPath = prefixDirectoryPath,
            FileName = fileName,
            ContentType = contentType
        };
    }

    public static PlatformFileStorageUploader Create(
        IFormFile formFile,
        [NotNull] string prefixDirectoryPath,
        string fileName = null,
        bool isPrivate = true,
        string contentType = null,
        string fileDescription = null)
    {
        return new PlatformFileStorageUploader
        {
            Stream = formFile.OpenReadStream(),
            RootDirectory = IPlatformFileStorageService.GetDefaultRootDirectoryName(isPrivate),
            PrefixDirectoryPath = prefixDirectoryPath,
            FileName = fileName ?? formFile.FileName,
            ContentType = contentType,
            FileDescription = fileDescription
        };
    }

    /// <summary>
    /// Create a path to storage content on cloud, this method will join each param with a <c>'/'</c>
    /// <para>NOTICE: best practice with Azure: {container}/{app}/{service}/{guid-com-id}/...</para>
    /// <para>Ex: private/talents/candidates/my-file.pdf</para>
    /// </summary>
    public static string CombinePath(params string[] elements)
    {
        return string.Join('/', elements.Where(el => !string.IsNullOrEmpty(el)));
    }
}
