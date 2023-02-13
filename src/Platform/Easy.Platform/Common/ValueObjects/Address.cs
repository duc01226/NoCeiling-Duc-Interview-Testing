using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.ValueObjects.Abstract;

namespace Easy.Platform.Common.ValueObjects;

public class Address : PlatformValueObject<Address>
{
    public Address() { }

    public Address(
        string streetNumber,
        string street,
        string ward,
        string district,
        string city,
        string state,
        string country,
        string zipCode)
    {
        StreetNumber = streetNumber;
        Street = street;
        Ward = ward;
        District = district;
        City = city;
        State = state;
        Country = country;
        ZipCode = zipCode;
    }

    public string StreetNumber { get; set; } = "";
    public string Street { get; set; } = "";
    public string Ward { get; set; } = "";
    public string District { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "";
    public string ZipCode { get; set; } = "";

    public override string ToString()
    {
        var joinedPartsString = $"{(StreetNumber.IsNullOrEmpty() ? "" : StreetNumber)}{(Street.IsNullOrEmpty() ? ", " : " " + Street + ", ")}" +
                                $"{(Ward.IsNullOrEmpty() ? "" : Ward + ", ")}" +
                                $"{(District.IsNullOrEmpty() ? "" : District + ", ")}" +
                                $"{(City.IsNullOrEmpty() ? "" : City + ", ")}" +
                                $"{(State.IsNullOrEmpty() ? "" : State + (ZipCode.IsNullOrEmpty() ? "" : " " + ZipCode) + ", ")}" +
                                $"{(Country.IsNullOrEmpty() ? "" : Country + ", ")}";

        return joinedPartsString.Trim().PipeIf(_ => _.EndsWith(","), _ => _.Substring(0, _.Length - 1));
    }

    public static implicit operator string(Address address)
    {
        return address.ToString();
    }
}
