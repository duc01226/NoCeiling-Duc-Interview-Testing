using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Dtos;

public interface IPlatformDto
{
}

public interface IPlatformDto<TDto> : IPlatformDto
    where TDto : IPlatformDto<TDto>
{
    PlatformValidationResult<TDto> Validate();
}

public interface IPlatformDto<TDto, out TMapForObject> : IPlatformDto<TDto>
    where TDto : IPlatformDto<TDto>
{
    TMapForObject MapToObject();
}
