#nullable enable
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Application.Dtos;

/// <summary>
/// Represents a Data Transfer Object (DTO) for platform entities.
/// </summary>
/// <remarks>
/// DTOs are used to shape the data that is sent between the client and server. This can be useful for:
/// - Removing circular references that can occur with Entity Framework entities and their navigation properties.
/// - Hiding properties that clients should not be able to view.
/// - Reducing payload size by omitting some properties.
/// - Decoupling the service layer from the database layer.
/// </remarks>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public abstract class PlatformEntityDto<TEntity, TId> : PlatformDto<TEntity>
    where TEntity : IEntity<TId>, new()
{
    public PlatformEntityDto() { }

    public PlatformEntityDto(TEntity entity)
    {
    }

    public override TEntity MapToObject()
    {
        return MapToEntity();
    }

    public new virtual PlatformValidationResult<PlatformEntityDto<TEntity, TId>> Validate()
    {
        return PlatformValidationResult<PlatformEntityDto<TEntity, TId>>.Valid(this);
    }

    /// <summary>
    /// The GetSubmittedId method is an abstract method declared in the PlatformEntityDto[TEntity, TId] class. This class is a Data Transfer Object (DTO) used to shape the data that is sent between the client and server. The GetSubmittedId method is designed to return the identifier of the DTO instance.
    /// <br />
    /// The purpose of this method is to provide a way to check whether the DTO is submitted for creation or update. This is used in the IsSubmitToUpdate method, which checks if the returned ID is null or a default value. If it is, the method returns false, indicating that the DTO is not submitted for update. If the ID is not null or a default value, the method checks the type of the ID and returns true if it's a non-empty string, a non-empty GUID, or a non-default numeric value.
    /// </summary>
    protected abstract object? GetSubmittedId();

    /// <summary>
    /// Creates a new entity of type TEntity and maps the properties of the current DTO to it.
    /// </summary>
    /// <returns>
    /// A new entity of type TEntity with properties mapped from the current DTO.
    /// </returns>
    public virtual TEntity MapToNewEntity()
    {
        var initialEntity = Activator.CreateInstance<TEntity>()
            .PipeAction(p => SetEntityId(p, MapToEntityModes.MapNewEntity));

        var updatedEntity = MapToEntity(initialEntity, MapToEntityModes.MapNewEntity);

        return updatedEntity;
    }

    /// <summary>
    /// Maps the properties of the DTO to a new instance of the entity.
    /// </summary>
    /// <returns>
    /// A new instance of the entity with properties mapped from the DTO.
    /// </returns>
    /// <remarks>
    /// The MapToEntity method is part of the PlatformEntityDto[TEntity, TId] class, which is an abstract class for Data Transfer Objects (DTOs) in the application. DTOs are objects that carry data between processes, in this case, likely between the application layers.
    /// <br />
    /// The MapToEntity method is used to map the properties of the DTO to a new instance of the entity. This is useful when you want to convert the data from the DTO, which is used for transferring data, to an entity, which is used within the application's domain logic.
    /// <br />
    /// In the provided code, the MapToEntity method is used in various places to convert DTOs to entities. For example, the MapToEntity method is used to convert a list of XXXEntityDto objects to a list of XXXEntity entities.
    /// <br />
    /// In the XXXEntity class, which inherits from PlatformEntityDto[TEntity, TId], the MapToEntity method is overridden to provide specific logic for mapping a XXXEntityDto to a XXXEntity entity.
    /// <br />
    /// In summary, the MapToEntity method is a crucial part of the application's data mapping strategy, allowing for easy conversion between DTOs and entities.
    /// </remarks>
    public virtual TEntity MapToEntity()
    {
        var initialEntity = Activator.CreateInstance<TEntity>()
            .PipeAction(p => SetEntityId(p, MapToEntityModes.MapAllProps));

        var updatedEntity = MapToEntity(initialEntity, MapToEntityModes.MapAllProps);

        return updatedEntity;
    }

    /// <summary>
    /// The MapToEntity method in the PlatformEntityDto[TEntity, TId] class is an abstract method that is designed to map the properties of the Data Transfer Object (DTO) to an entity of type TEntity. This method is used in the context of creating or updating entities based on the data contained in the DTO.
    /// <br />
    /// The method takes two parameters: an entity of type TEntity and a MapToEntityModes enum value. The TEntity parameter represents the entity that will be updated with the properties of the DTO. The MapToEntityModes parameter determines the mode of mapping, which can be either mapping all properties, mapping a new entity, or mapping to update an existing entity.
    /// <br />
    /// This method is overridden in each derived class, providing the specific implementation of how the properties of the DTO should be mapped to the properties of the specific entity.
    /// <br />
    /// In general, this method is essential for the process of transferring data from DTOs to entities, which is a common task in applications that follow the Domain-Driven Design (DDD) principles. This process is crucial for persisting data received from the client side into the database, or for updating the existing data in the database.
    /// </summary>
    protected abstract TEntity MapToEntity(TEntity entity, MapToEntityModes mode);

    /// <summary>
    /// Modify the toBeUpdatedEntity by apply current data from entity dto to the target toBeUpdatedEntity
    /// </summary>
    /// <returns>Return the modified toBeUpdatedEntity</returns>
    public virtual TEntity UpdateToEntity(TEntity toBeUpdatedEntity)
    {
        return MapToEntity(
            toBeUpdatedEntity
                .PipeAction(p => SetEntityId(p, MapToEntityModes.MapToUpdateExistingEntity)),
            MapToEntityModes.MapToUpdateExistingEntity);
    }

    public virtual bool HasSubmitId()
    {
        var submittedId = GetSubmittedId();

        if (submittedId == null) return false;
        if (submittedId is string strId) return !string.IsNullOrEmpty(strId);
        if (submittedId is Guid guidId) return guidId != Guid.Empty;
        if (submittedId is Ulid ulidId) return ulidId != Ulid.Empty;
        if (submittedId is long longId) return longId != default;
        if (submittedId is short shortId) return shortId != default;
        if (submittedId is int intId) return intId != default;

        // If value is a struct and value is equal the default value of the struct
        if (submittedId.GetType().IsValueType && submittedId.Equals(Activator.CreateInstance(submittedId.GetType()))) return false;

        return true;
    }

    public bool NotHasSubmitId()
    {
        return !HasSubmitId();
    }

    protected virtual void SetEntityId(TEntity entity, MapToEntityModes mode)
    {
        if (mode != MapToEntityModes.MapToUpdateExistingEntity)
            entity.Id = mode == MapToEntityModes.MapNewEntity || NotHasSubmitId() ? GenerateNewId() : (TId)GetSubmittedId()!;
#pragma warning disable S2955
        else if ((entity.Id == null || entity.Id.Equals(default(TId))) && GetSubmittedId() != default)
#pragma warning restore S2955
            entity.Id = (TId)GetSubmittedId()!;
    }

    protected abstract TId GenerateNewId();
}

public enum MapToEntityModes
{
    MapAllProps,
    MapNewEntity,
    MapToUpdateExistingEntity
}
