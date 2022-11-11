namespace Easy.Platform.Common.Exceptions;

public class PlatformNotFoundException : Exception
{
    public PlatformNotFoundException(string errorMsg, Exception innerException = null)
        : base(errorMsg, innerException)
    {
    }

    public PlatformNotFoundException(string errorMsg, Type objectType, Exception innerException = null)
        : base(BuildErrorMsg(objectType, errorMsg), innerException)
    {
    }

    public static string BuildErrorMsg(Type objectType, string errorMsg = null)
    {
        return errorMsg ?? $"{objectType.Name} is not found";
    }

    public static string BuildErrorMsg<TObject>(string errorMsg = null)
    {
        return BuildErrorMsg(typeof(TObject), errorMsg);
    }
}
