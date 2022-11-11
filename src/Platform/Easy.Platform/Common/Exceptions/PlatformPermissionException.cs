namespace Easy.Platform.Common.Exceptions;

public class PlatformPermissionException : Exception
{
    public PlatformPermissionException(string errorMsg, Exception innerException = null) : base(errorMsg, innerException)
    {
    }
}
