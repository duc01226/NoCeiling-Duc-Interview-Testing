using System.Diagnostics.CodeAnalysis;
using System.IO;
using Easy.Platform.Common.Validations;
using Easy.Platform.Infrastructures.Abstract;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.Infrastructures.FileStorage;

public interface IPlatformFileStorageService : IPlatformInfrastructureService
{
    public const string DefaultPrivateRootDirectoryName = "private";
    public const string DefaultPublicRootDirectoryName = "public";

    public static string GetDefaultRootDirectoryName(bool isPrivate)
    {
        return isPrivate
            ? DefaultPrivateRootDirectoryName
            : DefaultPublicRootDirectoryName;
    }

    public static PlatformFileStorageOptions.PublicAccessTypes GetDefaultPublicAccessType(bool isPrivate)
    {
        return isPrivate
            ? PlatformFileStorageOptions.PublicAccessTypes.None
            : PlatformFileStorageOptions.PublicAccessTypes.Container;
    }

    public static PlatformFileStorageOptions.PublicAccessTypes? GetDefaultRootDirectoryPublicAccessType(string rootDirectoryName)
    {
        return rootDirectoryName switch
        {
            DefaultPrivateRootDirectoryName => PlatformFileStorageOptions.PublicAccessTypes.None,
            DefaultPublicRootDirectoryName => PlatformFileStorageOptions.PublicAccessTypes.Container,
            _ => null
        };
    }

    /// <summary>
    /// Upload FormFile to cloud.
    /// </summary>
    Task<IPlatformFileStorageFileItem> UploadAsync(
        IFormFile formFile,
        [NotNull] string prefixDirectoryPath,
        bool isPrivate,
        string fileDescription = null,
        string fileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload file to cloud.
    /// </summary>
    Task<IPlatformFileStorageFileItem> UploadAsync(
        PlatformFileStorageUploader fileStorageUploader,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload stream content to cloud service and return storage path.
    /// </summary>
    Task<IPlatformFileStorageFileItem> UploadAsync(
        Stream contentStream,
        [NotNull] string rootDirectory,
        [NotNull] string filePath,
        PlatformFileStorageOptions.PublicAccessTypes? publicAccessType = null,
        string mimeContentType = null,
        string fileDescription = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if file exist
    /// </summary>
    Task<bool> ExistsAsync(
        [NotNull] string rootDirectory,
        [NotNull] string filePath);

    /// <summary>
    /// Remove a content from cloud storage with identifier path
    /// </summary>
    Task<bool> DeleteAsync(string fullFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a content from cloud storage with rootDirectory + filePath
    /// </summary>
    Task<bool> DeleteAsync(string rootDirectory, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stream of file content on the file storage service
    /// </summary>
    Task<Stream> GetStreamAsync(string fullFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stream of file content on the file storage service
    /// </summary>
    Task<Stream> GetStreamAsync(
        [NotNull] string rootDirectory,
        [NotNull] string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a access uri for non-public content
    /// </summary>
    Task<Uri> CreateSharedAccessUriAsync(string fullFilePath, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a access uri for non-public content
    /// </summary>
    Task<Uri> CreateSharedAccessUriAsync(string rootDirectory, string filePath, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copy file to new location
    /// </summary>
    Task<string> CopyFileAsync(
        string sourceFullFilePath,
        string destinationFullFilePath);

    /// <summary>
    /// Move file to new location
    /// </summary>
    Task<string> MoveFileAsync(string fullFilePath, string newLocationFullFilePath);

    string GetFileStorageEndpoint();

    IPlatformFileStorageDirectory GetDirectory(string rootDirectory, string directoryPath);

    IPlatformFileStorageFileItem GetFileItem(string rootDirectory, string filePath);

    PlatformValidationResult<string> ValidateFileName(string fileName);

    PlatformValidationResult<string> ValidateDirectoryName(string directoryName);

    Task UpdateFileDescriptionAsync(string rootDirectory, string filePath, string fileDescription);
}
