using Easy.Platform.Application.Dtos;
using Easy.Platform.Common.ValueObjects;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.ValueObjects;

namespace PlatformExampleApp.TextSnippet.Application.Dtos.EntityDtos;

public sealed class TextSnippetEntityDto : PlatformEntityDto<TextSnippetEntity, string>
{
    public TextSnippetEntityDto() { }

    public TextSnippetEntityDto(TextSnippetEntity entity)
    {
        Id = entity.Id;
        SnippetText = entity.SnippetText;
        FullText = entity.FullText;
        Address = entity.Address;
        CreatedDate = entity.CreatedDate;
        TimeOnly = entity.TimeOnly;
    }

    public string Id { get; set; }

    public string SnippetText { get; set; }

    public string FullText { get; set; }

    public TimeOnly? TimeOnly { get; set; }

    public ExampleAddressValueObject Address { get; set; }

    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Demo some common useful value object like Address
    /// </summary>
    public Address Address1 { get; set; }

    /// <summary>
    /// Demo some common useful value object like FullName
    /// </summary>
    public FullName FullName { get; set; }

    protected override object GetSubmittedId()
    {
        return Id;
    }

    protected override TextSnippetEntity MapToEntity(TextSnippetEntity entity, MapToEntityModes mode)
    {
        entity.SnippetText = SnippetText;
        entity.FullText = FullText;

        // Demo do not update address on submit. Only when create new entity or mapping data to return to client
        if (mode != MapToEntityModes.MapToUpdateExistingEntity)
            entity.Address = Address;
        entity.TimeOnly = TimeOnly ?? default;

        return entity;
    }

    protected override string GenerateNewId()
    {
        return Ulid.NewUlid().ToString();
    }
}
