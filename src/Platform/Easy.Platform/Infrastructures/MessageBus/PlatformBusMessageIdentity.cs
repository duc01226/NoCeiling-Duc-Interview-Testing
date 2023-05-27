namespace Easy.Platform.Infrastructures.MessageBus;

public class PlatformBusMessageIdentity
{
    /// <summary>
    /// Indicate which user id generate the message
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Indicate which request id generate the message
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// Indicate which user name generate the message
    /// </summary>
    public string UserName { get; set; }
}
