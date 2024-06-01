using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Easy.Platform.Infrastructures.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.AzureFileStorage;

public class PlatformAzureFileStorageService : IPlatformFileStorageService
{
    private readonly BlobServiceClient blobServiceClient;
    private readonly PlatformAzureFileStorageConfiguration fileStorageConfiguration;
    private readonly PlatformFileStorageOptions fileStorageOptions;
    private readonly ILogger logger;

    public PlatformAzureFileStorageService(
        PlatformAzureFileStorageConfiguration fileStorageConfigurationOptions,
        ILoggerFactory loggerFactory,
        BlobServiceClient blobServiceClient,
        PlatformFileStorageOptions fileStorageOptions)
    {
        logger = loggerFactory.CreateLogger(typeof(PlatformAzureFileStorageService).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}");
        this.blobServiceClient = blobServiceClient;
        this.fileStorageOptions = fileStorageOptions;
        fileStorageConfiguration = fileStorageConfigurationOptions;
    }

    public Task<IPlatformFileStorageFileItem> UploadAsync(
        PlatformFileStorageUploader fileStorageUploader,
        CancellationToken cancellationToken = default)
    {
        return UploadAsync(
            fileStorageUploader.Stream,
            fileStorageUploader.RootDirectory,
            $"{fileStorageUploader.PrefixDirectoryPath.TrimEnd('/')}/{fileStorageUploader.FileName}",
            fileStorageUploader.PublicAccessType,
            fileStorageUploader.ContentType,
            fileStorageUploader.FileDescription,
            cancellationToken);
    }

    public async Task<IPlatformFileStorageFileItem> UploadAsync(
        Stream contentStream,
        string rootDirectory,
        string filePath,
        PlatformFileStorageOptions.PublicAccessTypes? publicAccessType = null,
        string mimeContentType = null,
        string fileDescription = null,
        CancellationToken cancellationToken = default)
    {
        var pureFilePath = filePath.RemoveSpecialCharactersUri();

        var blobClient = GetBlobClient(rootDirectory, pureFilePath, publicAccessType);
        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = mimeContentType ?? PlatformFileMimeTypeMapper.Instance.GetMimeType(pureFilePath)
        };

        try
        {
            var response = await blobClient.UploadAsync(
                contentStream,
                blobHttpHeaders,
                metadata: PlatformAzureFileStorageFileItem.SetMetadata(
                    new Dictionary<string, string>(),
                    PlatformAzureFileStorageFileItem.BlobDescriptionKey,
                    fileDescription),
                cancellationToken: cancellationToken);

            if (response.GetRawResponse().IsError)
            {
                var statusCode = response.GetRawResponse().Status;
                var errContent = response.GetRawResponse().Content.ToString();

                logger.LogError(
                    "Fail to upload blob {PureFilePath} in {RootDirectory}, statusCode: {StatusCode} , content: {ErrContent}",
                    pureFilePath,
                    rootDirectory,
                    statusCode,
                    errContent);

                throw new Exception(errContent);
            }

            return new PlatformAzureFileStorageFileItem
            {
                RootDirectory = rootDirectory,
                FullFilePath = PlatformAzureFileStorageFileItem.GetFullFilePath(blobClient),
                AbsoluteUri = blobClient.Uri.AbsoluteUri,
                ContentType = blobHttpHeaders.ContentType,
                Etag = response.Value.ETag.ToString(),
                Size = contentStream.Length,
                Description = fileDescription
            };
        }
        catch (Exception e)
        {
            logger.LogError(e.BeautifyStackTrace(), "Fail to upload blob {PureFilePath} in {RootDirectory} container", pureFilePath, rootDirectory);
            throw;
        }
    }

    public Task<IPlatformFileStorageFileItem> UploadAsync(
        IFormFile formFile,
        string prefixDirectoryPath,
        bool isPrivate,
        string fileDescription = null,
        string fileName = null,
        CancellationToken cancellationToken = default)
    {
        var fileUploader = PlatformFileStorageUploader.Create(
            formFile,
            prefixDirectoryPath,
            rootDirectory: IPlatformFileStorageService.GetDefaultRootDirectoryName(isPrivate),
            publicAccessType: IPlatformFileStorageService.GetDefaultPublicAccessType(isPrivate),
            fileDescription: fileDescription,
            fileName: fileName);

        return UploadAsync(fileUploader, cancellationToken);
    }

    public Task<bool> ExistsAsync(string rootDirectory, string filePath)
    {
        var containerClient = GetAzureBlobContainerClient(rootDirectory, null);

        var pureFilePath = filePath.RemoveSpecialCharactersUri();
        var blobClient = containerClient.GetBlobClient(pureFilePath);

        return blobClient.ExistsAsync().Then(p => p.HasValue);
    }

    public Task<bool> DeleteAsync(string fullFilePath, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(fullFilePath);

        return DeleteAsync(blobClient, cancellationToken);
    }

    public Task<bool> DeleteAsync(string rootDirectory, string filePath, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(rootDirectory, filePath);

        return DeleteAsync(blobClient, cancellationToken);
    }

    public Task<Stream> GetStreamAsync(string fullFilePath, CancellationToken cancellationToken = default)
    {
        // Note: Files were stored in the public/ container before
        // After that, they were stored in the privite/ container which requires a security thing to read
        // Now, we have to generate SAS security to read any files in the container

        var blobClient = GetBlobClient(fullFilePath);
        var sharedAccessUri = CreateBlobSharedAccessUri(
            blobClient,
            fileStorageConfiguration.DefaultSharedPrivateFileUriAccessTimeMinutes.Minutes());
        blobClient = new BlobClient(sharedAccessUri);

        return blobClient.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public Task<Stream> GetStreamAsync(string rootDirectory, string filePath, CancellationToken cancellationToken = default)
    {
        var containerClient = GetAzureBlobContainerClient(rootDirectory, null);

        var pureFilePath = filePath.RemoveSpecialCharactersUri();
        var blobClient = containerClient.GetBlobClient(pureFilePath);

        return blobClient.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<Uri> CreateSharedAccessUriAsync(string fullFilePath, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(fullFilePath);

        return !await blobClient.ExistsAsync(cancellationToken)
            ? null
            : CreateBlobSharedAccessUri(blobClient, expirationTime ?? fileStorageConfiguration.DefaultSharedPrivateFileUriAccessTimeMinutes.Minutes());
    }

    public async Task<Uri> CreateSharedAccessUriAsync(
        string rootDirectory,
        string filePath,
        TimeSpan? expirationTime = null,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(rootDirectory, filePath);

        return !await blobClient.ExistsAsync(cancellationToken)
            ? null
            : CreateBlobSharedAccessUri(blobClient, expirationTime ?? fileStorageConfiguration.DefaultSharedPrivateFileUriAccessTimeMinutes.Minutes());
    }

    public async Task<string> CopyFileAsync(
        string sourceFullFilePath,
        string destinationFullFilePath)
    {
        var srcBlobClient = GetBlobClient(sourceFullFilePath);

        if (!await srcBlobClient.ExistsAsync())
        {
            logger.LogError("File '{SourceFullFilePath}' do not existed", sourceFullFilePath);
            return null;
        }

        // Lease the source blob for the copy operation 
        // to prevent another client from modifying it.
        var leaseClient = srcBlobClient.GetBlobLeaseClient();

        // Specifying -1 for the lease interval creates an infinite lease.
        await leaseClient.AcquireAsync(TimeSpan.FromSeconds(-1));

        var destBlobClient = GetBlobClient(destinationFullFilePath.RemoveSpecialCharactersUri());

        // Start copy operation
        await destBlobClient.StartCopyFromUriAsync(srcBlobClient.Uri).Then(_ => _.WaitForCompletionAsync());
        await destBlobClient.SetMetadataAsync(await srcBlobClient.GetPropertiesAsync().Then(_ => _.Value.Metadata));

        var sourceProperties = await srcBlobClient.GetPropertiesAsync().Then(_ => _.Value);

        if (sourceProperties.LeaseState == LeaseState.Leased)
            // Break the lease on the source blob.
            await leaseClient.BreakAsync();

        return destinationFullFilePath;
    }

    public async Task<string> MoveFileAsync(string fullFilePath, string newLocationFullFilePath)
    {
        var location = await CopyFileAsync(fullFilePath, newLocationFullFilePath);

        await DeleteAsync(fullFilePath);

        return location;
    }

    public string GetFileStorageEndpoint()
    {
        return blobServiceClient.Uri.AbsoluteUri;
    }

    public IPlatformFileStorageDirectory GetDirectory(
        string rootDirectory,
        string directoryPath)
    {
        return new PlatformAzureFileStorageDirectory(GetAzureBlobContainerClient(rootDirectory, null), directoryPath);
    }

    public IPlatformFileStorageFileItem GetFileItem(string rootDirectory, string filePath)
    {
        var blobClient = GetBlobClient(rootDirectory, filePath);
        var blobProperties = blobClient.GetProperties().Value;

        return new PlatformAzureFileStorageFileItem
        {
            RootDirectory = rootDirectory,
            FullFilePath = PlatformAzureFileStorageFileItem.GetFullFilePath(blobClient),
            AbsoluteUri = blobClient.Uri.AbsoluteUri,
            ContentType = PlatformFileMimeTypeMapper.Instance.GetMimeType(filePath),
            Description = PlatformAzureFileStorageFileItem.GetMetadata(blobProperties.Metadata, PlatformAzureFileStorageFileItem.BlobDescriptionKey),
            Etag = blobProperties.ETag.ToString(),
            Size = blobProperties.ContentLength,
            LastModified = blobProperties.LastModified
        };
    }

    public async Task UpdateFileDescriptionAsync(
        string rootDirectory,
        string filePath,
        string fileDescription)
    {
        var blobClient = GetBlobClient(rootDirectory, filePath);

        var currentMetadata = await blobClient.GetPropertiesAsync().Then(_ => _.Value.Metadata);

        if (!string.IsNullOrWhiteSpace(fileDescription))
            PlatformAzureFileStorageFileItem.SetMetadata(currentMetadata, PlatformAzureFileStorageFileItem.BlobDescriptionKey, fileDescription);
        else
            currentMetadata.Remove(PlatformAzureFileStorageFileItem.BlobDescriptionKey);

        var response = await blobClient.SetMetadataAsync(currentMetadata);

        var rawResponse = response.GetRawResponse();

        if (rawResponse.IsError)
        {
            var statusCode = rawResponse.Status;
            var errContent = rawResponse.Content.ToString();

            logger.LogError(
                "Fail to update file description {FilePath} in {RootDirectory}, statusCode: {StatusCode} , content: {ErrContent}",
                filePath,
                rootDirectory,
                statusCode,
                errContent);

            throw new Exception(errContent);
        }
    }

    public static PublicAccessType MapToAzurePublicAccessType(PlatformFileStorageOptions.PublicAccessTypes filePublicAccessType)
    {
        return filePublicAccessType switch
        {
            PlatformFileStorageOptions.PublicAccessTypes.None => PublicAccessType.None,
            PlatformFileStorageOptions.PublicAccessTypes.Container => PublicAccessType.BlobContainer,
            PlatformFileStorageOptions.PublicAccessTypes.File => PublicAccessType.Blob,
            _ => PublicAccessType.None
        };
    }

    /// <summary>
    /// Create a Sas Uri for blob for public access
    /// </summary>
    public static Uri CreateBlobSharedAccessUri(
        BlobClient blobClient,
        TimeSpan expirationTime)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expirationTime)
        };

        sasBuilder.SetPermissions(BlobAccountSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return sasUri;
    }

    private static async Task<bool> DeleteAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        return response.Value;
    }

    private BlobClient GetBlobClient(PlatformAzureBlobPathInfo blobInfo, PlatformFileStorageOptions.PublicAccessTypes? publicAccessType)
    {
        var blobContainerClient = GetAzureBlobContainerClient(blobInfo.ContainerName, publicAccessType);

        var blobClient = blobContainerClient.GetBlobClient(blobInfo.BlobName);

        return blobClient;
    }

    private BlobClient GetBlobClient(string fullFilePath)
    {
        var fileBlobPathInfo = PlatformAzureBlobPathInfo.Create(fullFilePath);

        return GetBlobClient(fileBlobPathInfo, null);
    }

    private BlobClient GetBlobClient(string rootDirectory, string filePath, PlatformFileStorageOptions.PublicAccessTypes? publicAccessType = null)
    {
        return GetBlobClient(PlatformAzureBlobPathInfo.Create(rootDirectory, filePath), publicAccessType);
    }

    private BlobContainerClient GetAzureBlobContainerClient(
        string containerName,
        PlatformFileStorageOptions.PublicAccessTypes? publicAccessType)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var azurePublicAccessType = GetAzureContainerPublicAccessType(containerName, publicAccessType);

        containerClient.CreateIfNotExistsAsync(azurePublicAccessType);

        return containerClient;
    }

    private PublicAccessType GetAzureContainerPublicAccessType(string containerName, PlatformFileStorageOptions.PublicAccessTypes? filePublicAccessType)
    {
        if (filePublicAccessType != null) return MapToAzurePublicAccessType(filePublicAccessType.Value);

        return MapToAzurePublicAccessType(
            IPlatformFileStorageService.GetDefaultRootDirectoryPublicAccessType(containerName) ??
            fileStorageOptions.DefaultFileAccessType);
    }

    #region ValidateFileName

    // Copy from Microsoft.Azure.Storage.NameValidator

    private static readonly RegexOptions RegexOptions = RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant;
    private static readonly Regex FileDirectoryRegex = new("^[^\"\\\\/:|<>*?]*\\/{0,1}$", RegexOptions);

    private static readonly string[] ReservedFileNames =
    [
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
    ];

    private static void EnsureFileResourceNameValid(string resourceName, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. The {0} name may not be null, empty, or whitespace only.", resourceType));
        if (resourceName.Length < 1 || resourceName.Length > byte.MaxValue)
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid {0} name length. The {0} name must be between {1} and {2} characters long.",
                    resourceType,
                    1,
                    (int)byte.MaxValue));
        if (!FileDirectoryRegex.IsMatch(resourceName))
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. Check MSDN for more information about valid {0} naming.", resourceType));
    }

    public PlatformValidationResult<string> ValidateFileName(string fileName)
    {
        try
        {
            // Copy from Microsoft.Azure.Storage.NameValidator
            EnsureFileResourceNameValid(fileName, "file");
            if (fileName.EndsWith('/'))
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. Check MSDN for more information about valid {0} naming.", "file"));
            if (ReservedFileNames.Any(p => p.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. This {0} name is reserved.", "file"));

            return PlatformValidationResult.Valid(fileName);
        }
        catch
        {
            return PlatformValidationResult.Invalid(fileName, "Invalid file name");
        }
    }

    public PlatformValidationResult<string> ValidateDirectoryName(string directoryName)
    {
        try
        {
            EnsureFileResourceNameValid(directoryName, "directory");
            return PlatformValidationResult.Valid(directoryName);
        }
        catch
        {
            return PlatformValidationResult.Invalid(directoryName, "Invalid directory name");
        }
    }

    #endregion
}
