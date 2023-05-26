namespace Easy.Platform.Domain.Exceptions;

public sealed class PlatformDomainRowVersionConflictException : PlatformDomainException
{
    public PlatformDomainRowVersionConflictException(string errorMsg, Exception innerException = null) : base(
        errorMsg,
        innerException)
    {
    }
}
