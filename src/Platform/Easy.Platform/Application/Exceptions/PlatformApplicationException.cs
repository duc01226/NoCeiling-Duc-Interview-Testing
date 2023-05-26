namespace Easy.Platform.Application.Exceptions;

public sealed class PlatformApplicationException : Exception
{
    public PlatformApplicationException(string message) : base(message)
    {
    }
}
