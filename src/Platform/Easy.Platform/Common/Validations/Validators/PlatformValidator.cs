using Easy.Platform.Common.Extensions;
using FluentValidation;

namespace Easy.Platform.Common.Validations.Validators;

public class PlatformValidator<T> : AbstractValidator<T>
{
    public static PlatformValidationResult Invalid<TObjectTarget>(TObjectTarget objectTarget, string property, string errorMsg)
    {
        return PlatformValidationResult<TObjectTarget>.Invalid(objectTarget, new PlatformValidationError(property, errorMsg));
    }

    public static PlatformValidationResult Valid<TObjectTarget>(TObjectTarget objectTarget)
    {
        return PlatformValidationResult<TObjectTarget>.Valid(objectTarget);
    }

    public static PlatformValidator<T> Create()
    {
        return new PlatformValidator<T>();
    }

    public static PlatformValidator<T> Create(params PlatformValidator<T>[] includeValidators)
    {
        var result = new PlatformValidator<T>();

        includeValidators.ForEach(platformValidator => result.Include(platformValidator));

        return result;
    }

    public static PlatformValidator<T> Create(params PlatformSingleValidator<T, object>[] includeValidators)
    {
        return Create(includeValidators.Select(p => (PlatformValidator<T>)p).ToArray());
    }

    public override PlatformValidationResult Validate(ValidationContext<T> context)
    {
        var validationResult = base.Validate(context);

        return new PlatformValidationResult<object>(
            context.InstanceToValidate,
            validationResult.Errors.Select(p => new PlatformValidationError(p)).ToList());
    }

    public new PlatformValidationResult<T> Validate(T instance)
    {
        return new PlatformValidationResult<T>(
            instance,
            base.Validate(instance).Errors.Select(p => new PlatformValidationError(p)).ToList());
    }
}
