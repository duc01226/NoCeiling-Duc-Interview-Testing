using Easy.Platform.Common.Validations.Validators;
using Easy.Platform.Common.ValueObjects.Abstract;

namespace PlatformExampleApp.TextSnippet.Domain.ValueObjects;

public class ExampleAddressValueObject : PlatformValueObject<ExampleAddressValueObject>
{
    public string Number { get; set; } = "Default Number 123";
    public string Street { get; set; } = "Default Street Abc";

    public static PlatformExpressionValidator<ExampleAddressValueObject> NumberValidator()
    {
        return new PlatformExpressionValidator<ExampleAddressValueObject>(
            p => !string.IsNullOrEmpty(p.Number),
            "Number must be not null or empty");
    }

    public static PlatformExpressionValidator<ExampleAddressValueObject> StreetValidator()
    {
        return new PlatformExpressionValidator<ExampleAddressValueObject>(
            p => !string.IsNullOrEmpty(p.Street),
            "Street must be not null or empty");
    }

    public static PlatformValidator<ExampleAddressValueObject> GetValidator()
    {
        return PlatformValidator<ExampleAddressValueObject>.Create(NumberValidator(), StreetValidator());
    }

    public override PlatformValidationResult<ExampleAddressValueObject> Validate()
    {
        return GetValidator().Validate(this);
    }
}
