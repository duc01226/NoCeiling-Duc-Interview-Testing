using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Extensions.WhenCases;
using Easy.Platform.Common.Validations;
using Easy.Platform.Common.ValueObjects.Abstract;

namespace Easy.Platform.Common.ValueObjects;

public sealed class FullName : PlatformValueObject<FullName>
{
    public FullName()
    {
    }

    public FullName(string firstName, string middleName, string lastName)
    {
        FirstName = firstName;
        MiddleName = middleName;
        LastName = lastName;
    }

    /// <summary>
    /// Your called name every day
    /// </summary>
    public string FirstName { get; set; } = "";

    /// <summary>
    /// Optional
    /// </summary>
    public string MiddleName { get; set; } = "";

    /// <summary>
    /// Family Name
    /// </summary>
    public string LastName { get; set; } = "";

    public static FullName New(string firstName = "", string middleName = "", string lastName = "")
    {
        return new FullName(firstName, middleName, lastName);
    }

    public static FullName FromUSFormatString(string fullNameString)
    {
        var parts = fullNameString.Split(" ");

        if (parts.Length == 0) return new FullName();

        var firstName = parts.Length
            .When(partsLength => partsLength >= 2, _ => parts.First())
            .Else(partsLength => "")
            .Execute();
        var middleName = parts.Length
            .When(partsLength => partsLength >= 3, _ => parts.Skip(1).Take(parts.Length - 2).JoinToString(" "))
            .Else(partsLength => "")
            .Execute();
        var lastName = parts.Last();

        return new FullName(firstName, middleName, lastName);
    }

    public static FullName FromAsianFormatString(string fullNameString)
    {
        var parts = fullNameString.Split(" ");

        if (parts.Length == 0) return new FullName();

        var firstName = parts.Last();
        var middleName = parts.Length
            .When(partsLength => partsLength >= 3, _ => parts.Skip(1).Take(parts.Length - 2).JoinToString(" "))
            .Else(partsLength => "")
            .Execute();
        var lastName = parts.Length
            .When(partsLength => partsLength >= 2, _ => parts.First())
            .Else(partsLength => "")
            .Execute();

        return new FullName(firstName, middleName, lastName);
    }

    public override PlatformValidationResult<FullName> Validate()
    {
        return base.Validate()
            .And(_ => !MiddleName.IsNullOrEmpty() && FirstName.IsNullOrEmpty() && LastName.IsNullOrEmpty(), "FirstName and LastName must be given with MiddleName");
    }

    public static implicit operator string(FullName fullName)
    {
        return fullName?.ToString();
    }

    public override string ToString()
    {
        return ToUSFormatString();
    }

    public string ToUSFormatString()
    {
        var joinedPartsString = $"{(FirstName.IsNullOrEmpty() ? "" : FirstName + " ")}" +
                                $"{(MiddleName.IsNullOrEmpty() ? "" : MiddleName + " ")}" +
                                $"{(LastName.IsNullOrEmpty() ? "" : LastName + " ")}";

        return joinedPartsString.Trim();
    }

    public static string ToUSFormatString(string firstName, string middleName, string lastName)
    {
        return new FullName(firstName, middleName, lastName).ToUSFormatString();
    }

    public string ToAsianFormatString()
    {
        var joinedPartsString = $"{(LastName.IsNullOrEmpty() ? "" : LastName + " ")}" +
                                $"{(MiddleName.IsNullOrEmpty() ? "" : MiddleName + " ")}" +
                                $"{(FirstName.IsNullOrEmpty() ? "" : FirstName + " ")}";

        return joinedPartsString.Trim();
    }

    public static string ToAsianFormatString(string firstName, string middleName, string lastName)
    {
        return new FullName(firstName, middleName, lastName).ToString();
    }
}
