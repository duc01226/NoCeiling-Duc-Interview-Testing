using Easy.Platform.Application.Exceptions;
using Easy.Platform.Common.Exceptions;
using Easy.Platform.Domain.Exceptions;

namespace Easy.Platform.AspNetCore.ExceptionHandling;

public sealed class PlatformAspNetMvcErrorInfo
{
    private const string DefaultServerErrorMessage =
        "There is an unexpected error during the processing of the request. Please try again or contact the Administrator for help.";

    /// <summary>
    ///     One of a server-defined set of error types.
    /// </summary>
    public string Code { get; set; }

    public string Message { get; set; }

    public string DeveloperExceptionMessage { get; set; }

    public Dictionary<string, object> FormattedMessagePlaceholderValues { get; set; }

    /// <summary>
    ///     The target of the error.
    /// </summary>
    public string Target { get; set; }

    public List<PlatformAspNetMvcErrorInfo> Details { get; set; }

    public static PlatformAspNetMvcErrorInfo FromUnknownException(
        Exception exception,
        bool developerExceptionEnabled)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = "InternalServerException",
            Message = DefaultServerErrorMessage,
            DeveloperExceptionMessage = developerExceptionEnabled ? exception.ToString() : null
        };
    }

    public static PlatformAspNetMvcErrorInfo FromValidationException(
        IPlatformValidationException validationException,
        bool developerExceptionEnabled)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = validationException.GetType().Name,
            Message = validationException.Message,
            DeveloperExceptionMessage = developerExceptionEnabled ? validationException.ToString() : null,
            Details = validationException.ValidationResult.AggregateErrors()
                .Select(
                    p => new PlatformAspNetMvcErrorInfo
                    {
                        Code = p.ErrorCode,
                        Message = p.ErrorMessage,
                        Target = p.PropertyName,
                        FormattedMessagePlaceholderValues = p.FormattedMessagePlaceholderValues
                    })
                .ToList()
        };
    }

    public static PlatformAspNetMvcErrorInfo FromApplicationException(
        PlatformApplicationException applicationException,
        bool developerExceptionEnabled)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = applicationException.GetType().Name,
            Message = applicationException.Message,
            DeveloperExceptionMessage = developerExceptionEnabled ? applicationException.ToString() : null
        };
    }

    public static PlatformAspNetMvcErrorInfo FromDomainException(
        PlatformDomainException domainException,
        bool developerExceptionEnabled)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = domainException.GetType().Name,
            Message = domainException.Message,
            DeveloperExceptionMessage = developerExceptionEnabled ? domainException.ToString() : null
        };
    }

    public static PlatformAspNetMvcErrorInfo FromPermissionException(
        PlatformPermissionException permissionException,
        bool developerExceptionEnabled)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = permissionException.GetType().Name,
            Message = permissionException.Message,
            DeveloperExceptionMessage = developerExceptionEnabled ? permissionException.ToString() : null
        };
    }

    public static PlatformAspNetMvcErrorInfo FromNotFoundException(
        PlatformNotFoundException domainNotFoundException,
        bool developerExceptionEnabled)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = nameof(PlatformNotFoundException),
            Message = domainNotFoundException.Message,
            DeveloperExceptionMessage = developerExceptionEnabled ? domainNotFoundException.ToString() : null
        };
    }
}
