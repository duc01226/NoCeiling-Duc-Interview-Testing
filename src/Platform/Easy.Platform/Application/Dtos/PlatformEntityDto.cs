#nullable enable
using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Application.Dtos;

/// <summary>
/// This represent entity data to be sent and return between client and server
/// Why do we want dto?
/// Sometimes you want to change the shape of the data that you send to client. For example, you might want to:
/// - Remove circular references (entity framework entity, relation between entities present via navigation property)
/// - Hide particular properties that clients are not supposed to view.
/// - Omit some properties in order to reduce payload size.
/// - Decouple your service layer from your database layer.
/// </summary>
public abstract class PlatformEntityDto<TEntity, TId> : IPlatformDto<PlatformEntityDto<TEntity, TId>>
    where TEntity : IEntity<TId>, new()
{
    public PlatformEntityDto() { }

    public PlatformEntityDto(TEntity entity)
    {
    }

    public virtual PlatformValidationResult<PlatformEntityDto<TEntity, TId>> Validate()
    {
        return PlatformValidationResult<PlatformEntityDto<TEntity, TId>>.Valid(this);
    }

    /// <summary>
    /// Return the defined Id from inherit dto. This function is used to check that the dto is submitted to create or edit in IsSubmitToUpdate function
    /// </summary>
    /// <returns></returns>
    protected abstract object? GetSubmittedId();

    /// <summary>
    /// Map to create new entity
    /// </summary>
    /// <returns></returns>
    public virtual TEntity MapToNewEntity()
    {
        var initialEntity = Activator.CreateInstance<TEntity>();

        var updatedEntity = MapToEntity(initialEntity, MapToEntityModes.MapNewEntity);

        return updatedEntity;
    }

    /// <summary>
    /// Map all props
    /// </summary>
    public virtual TEntity MapToEntity()
    {
        var initialEntity = Activator.CreateInstance<TEntity>();

        var updatedEntity = MapToEntity(initialEntity, MapToEntityModes.MapAllProps);

        return updatedEntity;
    }

    protected abstract TEntity MapToEntity(TEntity entity, MapToEntityModes mode);

    /// <summary>
    /// Modify the toBeUpdatedEntity by apply current data from entity dto to the target toBeUpdatedEntity
    /// </summary>
    /// <returns>Return the modified toBeUpdatedEntity</returns>
    public virtual TEntity UpdateToEntity(TEntity toBeUpdatedEntity)
    {
        return MapToEntity(toBeUpdatedEntity, MapToEntityModes.MapToUpdateExistingEntity);
    }

    public virtual bool IsSubmitToUpdate()
    {
        if (GetSubmittedId() == null || GetSubmittedId() == default) return false;

        return GetSubmittedId() switch
        {
            string strId => strId.IsNotNullOrEmpty(),
            Guid guidId => guidId != Guid.Empty,
            long longId => longId != default,
            int intId => intId != default,
            _ => false
        };
    }

    public virtual bool IsSubmitToCreate()
    {
        return !IsSubmitToUpdate();
    }
}

public enum MapToEntityModes
{
    MapAllProps,
    MapNewEntity,
    MapToUpdateExistingEntity
}
