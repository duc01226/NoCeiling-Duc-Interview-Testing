namespace Easy.Platform.Domain.Exceptions;

public class PlatformDomainRowVersionConflictException : PlatformDomainException
{
    public PlatformDomainRowVersionConflictException(string errorMsg, Exception innerException = null) : base(
        errorMsg,
        innerException)
    {
    }
}
