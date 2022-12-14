namespace Easy.Platform.Infrastructures.FileStorage;

public class PlatformFileStorageOptions
{
    /// <summary>
    /// Config DefaultFileAccessType for any other file root containers
    /// </summary>
    public PublicAccessTypes DefaultFileAccessType { get; set; } = PublicAccessTypes.None;

    /// <summary>
    /// Specifies whether data in the container may be accessed publicly and the level of access.
    /// </summary>
    public enum PublicAccessTypes
    {
        None,

        /// <summary>
        /// Container and file data can be read via anonymous request. Clients can enumerate files within the container via anonymous request, but cannot enumerate other containers within the storage account.
        /// </summary>
        Container,

        /// <summary>
        ///  File data within this container can be read via anonymous request, but container data is not available. Clients cannot enumerate files within the container via anonymous request.
        /// </summary>
        File
    }
}
