using Easy.Platform.Common.Validations;
using Easy.Platform.Common.Validations.Exceptions;

namespace Easy.Platform.Domain.Exceptions;

public sealed class PlatformDomainValidationException : PlatformDomainException, IPlatformValidationException
{
    public PlatformDomainValidationException(PlatformValidationResult validationResult) : base(
        validationResult.ToString())
    {
        ValidationResult = validationResult;
    }

    public PlatformValidationResult ValidationResult { get; set; }
}
