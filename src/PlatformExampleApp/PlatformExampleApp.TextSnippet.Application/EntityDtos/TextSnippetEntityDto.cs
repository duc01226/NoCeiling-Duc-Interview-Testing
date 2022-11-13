using Easy.Platform.Application.Dtos;
using Easy.Platform.Common.ValueObjects;
using PlatformExampleApp.TextSnippet.Application.ValueObjectDtos;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.EntityDtos;

public class TextSnippetEntityDto : PlatformEntityDto<TextSnippetEntity, Guid>
{
    public TextSnippetEntityDto() { }

    public TextSnippetEntityDto(TextSnippetEntity entity)
    {
        Id = entity.Id;
        SnippetText = entity.SnippetText;
        FullText = entity.FullText;
        Address = entity.Address != null ? ExampleAddressValueObjectDto.Create(entity.Address) : null;
        CreatedDate = entity.CreatedDate;
    }

    public Guid? Id { get; set; }

    public string SnippetText { get; set; }

    public string FullText { get; set; }

    public ExampleAddressValueObjectDto Address { get; set; }

    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Demo some common useful value object like Address
    /// </summary>
    public Address Address1 { get; set; }

    /// <summary>
    /// Demo some common useful value object like FullName
    /// </summary>
    public FullName FullName { get; set; }

    public override TextSnippetEntity UpdateToEntity(TextSnippetEntity toBeUpdatedEntity)
    {
        if (toBeUpdatedEntity.Id == Guid.Empty)
            toBeUpdatedEntity.Id = Id == Guid.Empty || Id == null ? Guid.NewGuid() : Id.Value;
        toBeUpdatedEntity.SnippetText = SnippetText;
        toBeUpdatedEntity.FullText = FullText;
        toBeUpdatedEntity.Address = Address?.MapToObject();

        return toBeUpdatedEntity;
    }
}
