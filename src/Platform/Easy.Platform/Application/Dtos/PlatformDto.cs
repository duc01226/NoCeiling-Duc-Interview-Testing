#nullable enable
using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Application.Dtos;

public abstract class PlatformDto<TMapForObject> : IPlatformDto<PlatformDto<TMapForObject>, TMapForObject>
{
    public virtual PlatformValidationResult<PlatformDto<TMapForObject>> Validate()
    {
        return PlatformValidationResult<PlatformDto<TMapForObject>>.Valid(this);
    }

    public abstract TMapForObject MapToObject();
}
