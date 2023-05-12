using Easy.Platform.Common.Exceptions;
using Easy.Platform.Common.Validations.Exceptions;
using Easy.Platform.Domain.Exceptions;

namespace Easy.Platform.Application.Exceptions.Extensions;

public static class GeneralPlatformLogicExceptionExtension
{
    public static bool IsPlatformLogicException(this Exception ex)
    {
        return ex is PlatformPermissionException ||
               ex is PlatformNotFoundException ||
               ex is PlatformApplicationException ||
               ex is PlatformDomainException ||
               ex is IPlatformValidationException;
    }
}
