using System.Security.Cryptography;
using System.Text;
using Easy.Platform.Common.Validations.Validators;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Exceptions.Extensions;
using FluentValidation;
using PlatformExampleApp.TextSnippet.Domain.ValueObjects;
using static Easy.Platform.Domain.Entities.ISupportDomainEventsEntity;

namespace PlatformExampleApp.TextSnippet.Domain.Entities;

public class TextSnippetEntity : RootAuditedEntity<TextSnippetEntity, Guid, Guid?>, IRowVersionEntity
{
    public const int FullTextMaxLength = 4000;
    public const int SnippetTextMaxLength = 100;

    public string SnippetText { get; set; }

    public string FullText { get; set; }

    public TimeOnly TimeOnly { get; set; } = TimeOnly.MaxValue;

    /// <summary>
    /// Demo ForeignKey for TextSnippetAssociatedEntity
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    public ExampleAddressValueObject Address { get; set; } = new()
    {
        Street = "Random default street"
    };

    public List<string> AddressStrings { get; set; } = new()
    {
        "Random default street",
        "Random default streetAB",
        "Random default streetCD"
    };

    public List<ExampleAddressValueObject> Addresses { get; set; } = new()
    {
        new ExampleAddressValueObject
        {
            Street = "Random default street"
        },
        new ExampleAddressValueObject
        {
            Street = "Random default streetAB"
        },
        new ExampleAddressValueObject
        {
            Street = "Random default streetCD"
        }
    };

    public Guid? ConcurrencyUpdateToken { get; set; }

    public override PlatformCheckUniqueValidator<TextSnippetEntity> CheckUniqueValidator()
    {
        return new PlatformCheckUniqueValidator<TextSnippetEntity>(
            targetItem: this,
            findOtherDuplicatedItemExpr: otherItem =>
                !otherItem.Id.Equals(Id) && otherItem.SnippetText == SnippetText,
            "SnippetText must be unique");
    }

    public TextSnippetEntity DemoDoSomeDomainEntityLogicAction_EncryptSnippetText()
    {
        var originalSnippetText = SnippetText;

        var bytes = Encoding.UTF8.GetBytes(SnippetText);
        var hash = SHA256.HashData(bytes);

        SnippetText = Convert.ToBase64String(hash);

        AddDomainEvents(
            new EncryptSnippetTextDomainEvent
            {
                OriginalSnippetText = originalSnippetText,
                EncryptedSnippetText = SnippetText
            });

        return this;
    }

    public class EncryptSnippetTextDomainEvent : DomainEvent
    {
        public string OriginalSnippetText { get; set; }

        public string EncryptedSnippetText { get; set; }
    }

    #region Basic Prop Validators

    public static PlatformSingleValidator<TextSnippetEntity, string> SnippetTextValidator()
    {
        return new PlatformSingleValidator<TextSnippetEntity, string>(
            p => p.SnippetText,
            p => p.NotNull().NotEmpty().MaximumLength(SnippetTextMaxLength));
    }

    public static PlatformSingleValidator<TextSnippetEntity, string> FullTextValidator()
    {
        return new PlatformSingleValidator<TextSnippetEntity, string>(
            p => p.FullText,
            p => p.NotNull().NotEmpty().MaximumLength(FullTextMaxLength));
    }

    public static PlatformSingleValidator<TextSnippetEntity, ExampleAddressValueObject> AddressValidator()
    {
        return new PlatformSingleValidator<TextSnippetEntity, ExampleAddressValueObject>(
            p => p.Address,
            p => p.SetValidator(ExampleAddressValueObject.GetValidator()));
    }

    public override PlatformValidator<TextSnippetEntity> GetValidator()
    {
        return PlatformValidator<TextSnippetEntity>.Create(
            SnippetTextValidator(),
            FullTextValidator(),
            AddressValidator());
    }

    #endregion

    #region Demo Validation Logic, Reuse logic and Expression

    public static PlatformExpressionValidator<TextSnippetEntity> SavePermissionValidator(Guid? userId)
    {
        return new PlatformExpressionValidator<TextSnippetEntity>(
            must: p => p.CreatedByUserId == null || userId == null || p.CreatedByUserId == userId,
            errorMessage: "User must be the creator to update text snippet entity");
    }

    public static PlatformExpressionValidator<TextSnippetEntity> SomeSpecificIsXxxLogicValidator()
    {
        return new PlatformExpressionValidator<TextSnippetEntity>(
            must: p => true,
            errorMessage: "Some example domain logic violated message.");
    }

    public PlatformValidationResult<TextSnippetEntity> ValidateSomeSpecificIsXxxLogic()
    {
        return SomeSpecificIsXxxLogicValidator().Validate(this).WithDomainException();
    }

    public PlatformValidationResult<TextSnippetEntity> ValidateSavePermission(Guid? userId)
    {
        return SavePermissionValidator(userId).Validate(this).WithPermissionException();
    }

    #endregion
}
