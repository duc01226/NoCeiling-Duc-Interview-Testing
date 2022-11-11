using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.DeprecatedFPLibrary;

public class Error
{
    internal Error(string message)
    {
        Message = message;
    }

    internal Error(string message, string code = null)
    {
        Message = message;
        Code = code;
    }

    internal Error(string message, int? httpStatus = null)
    {
        Message = message;
        HttpStatus = httpStatus;
    }

    protected Error()
    {
    }

    public virtual string Message { get; }

    public string Code { get; }

    public int? HttpStatus { get; }

    public static implicit operator Error(string m)
    {
        return new Error(m);
    }

    public static implicit operator Error((string, string) data)
    {
        return new Error(data.Item1, data.Item2);
    }

    public static implicit operator Error((string, int) data)
    {
        return new Error(data.Item1, data.Item2);
    }

    public static implicit operator PlatformValidationError(Error error)
    {
        return error.ToString();
    }

    public override string ToString()
    {
        return Message;
    }
}
