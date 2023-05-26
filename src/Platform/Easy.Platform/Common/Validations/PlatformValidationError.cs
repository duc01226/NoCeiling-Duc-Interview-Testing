using FluentValidation.Results;

namespace Easy.Platform.Common.Validations;

public sealed class PlatformValidationError : ValidationFailure
{
    public PlatformValidationError(ValidationFailure failure) : base(
        failure.PropertyName,
        failure.ErrorMessage,
        failure.AttemptedValue)
    {
        ErrorCode = failure.ErrorCode;
        CustomState = failure.CustomState;
        FormattedMessagePlaceholderValues = failure.FormattedMessagePlaceholderValues;
        Severity = failure.Severity;
    }

    public PlatformValidationError(string propertyName, string errorMessage) : base(propertyName, errorMessage)
    {
    }

    public PlatformValidationError(string propertyName, string errorMessage, object attemptedValue) : base(
        propertyName,
        errorMessage,
        attemptedValue)
    {
    }

    public static implicit operator string(PlatformValidationError validationError)
    {
        return validationError.ToString();
    }

    public static implicit operator PlatformValidationError(string msg)
    {
        return Create(msg);
    }

    public static PlatformValidationError Create(string message, Dictionary<string, string> messageParams = null)
    {
        return Create(message, null, messageParams);
    }

    public static PlatformValidationError Create(
        string message,
        string propName,
        Dictionary<string, string> messageParams = null)
    {
        return new PlatformValidationError(propName, message)
        {
            FormattedMessagePlaceholderValues = messageParams?.ToDictionary(p => p.Key, p => (object)p.Value)
        };
    }
}
