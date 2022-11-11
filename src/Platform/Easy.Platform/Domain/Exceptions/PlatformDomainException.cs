namespace Easy.Platform.Domain.Exceptions;

public class PlatformDomainException : Exception
{
    public PlatformDomainException(string errorMsg, Exception innerException = null) : base(errorMsg, innerException)
    {
    }
}
