namespace Easy.Platform.Common.Validations.Exceptions;

public interface IPlatformValidationException
{
    public string Message { get; }
    public PlatformValidationResult ValidationResult { get; set; }
}

public interface IPlatformValidationException<TValue> : IPlatformValidationException
{
    public new PlatformValidationResult<TValue> ValidationResult { get; set; }
}

public class PlatformValidationException<TValue> : Exception, IPlatformValidationException<TValue>
{
    public PlatformValidationException(PlatformValidationResult<TValue> validationResult) : base(
        validationResult.ToString())
    {
        ValidationResult = validationResult;
    }

    public PlatformValidationResult<TValue> ValidationResult { get; set; }

    PlatformValidationResult IPlatformValidationException.ValidationResult
    {
        get => ValidationResult;
        set => ValidationResult = value?.Of(value.Value is TValue validTypeValue ? validTypeValue : default);
    }
}

public class PlatformValidationException : PlatformValidationException<object>
{
    public PlatformValidationException(PlatformValidationResult validationResult) : base(
        new PlatformValidationResult<object>(validationResult.Value, validationResult.Errors))
    {
        ValidationResult = validationResult;
    }
}
