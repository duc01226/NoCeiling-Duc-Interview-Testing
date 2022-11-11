using Easy.Platform.Application.Exceptions;
using Easy.Platform.Common.Exceptions;
using Easy.Platform.Domain.Exceptions;

namespace Easy.Platform.AspNetCore.ExceptionHandling;

public class PlatformAspNetMvcErrorInfo
{
    /// <summary>
    ///     One of a server-defined set of error types.
    /// </summary>
    public string Code { get; set; }

    public string Message { get; set; }

    public Dictionary<string, object> FormattedMessagePlaceholderValues { get; set; }

    /// <summary>
    ///     The target of the error.
    /// </summary>
    public string Target { get; set; }

    public List<PlatformAspNetMvcErrorInfo> Details { get; set; }

    public static PlatformAspNetMvcErrorInfo FromValidationException(
        IPlatformValidationException validationException)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = validationException.GetType().Name,
            Message = validationException.Message,
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
        PlatformApplicationException applicationException)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = applicationException.GetType().Name,
            Message = applicationException.Message
        };
    }

    public static PlatformAspNetMvcErrorInfo FromDomainException(
        PlatformDomainException domainException)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = domainException.GetType().Name,
            Message = domainException.Message
        };
    }

    public static PlatformAspNetMvcErrorInfo FromPermissionException(
        PlatformPermissionException permissionException)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = permissionException.GetType().Name,
            Message = permissionException.Message
        };
    }

    public static PlatformAspNetMvcErrorInfo FromNotFoundException(
        PlatformNotFoundException domainNotFoundException)
    {
        return new PlatformAspNetMvcErrorInfo
        {
            Code = nameof(PlatformNotFoundException),
            Message = domainNotFoundException.Message
        };
    }
}
