using Easy.Platform.Application.Dtos;
using PlatformExampleApp.TextSnippet.Domain.ValueObjects;

namespace PlatformExampleApp.TextSnippet.Application.Dtos;

public class ExampleAddressValueObjectDto : PlatformDto<ExampleAddressValueObject>
{
    public ExampleAddressValueObjectDto() { }

    public ExampleAddressValueObjectDto(ExampleAddressValueObject obj)
    {
        Number = obj.Number;
        Street = obj.Street;
    }

    public string Number { get; set; }
    public string Street { get; set; }

    public override ExampleAddressValueObject MapToObject()
    {
        return new ExampleAddressValueObject
        {
            Number = Number,
            Street = Street
        };
    }
}
